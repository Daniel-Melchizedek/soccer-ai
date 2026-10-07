using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vog.Contracts;
using Vog.Describe;
using Vog.Domain;
using Vog.Pipeline;
using Vog.Stats;

namespace Vog.Companion;

/// <summary>A function the companion model may call: name, what it's for, JSON schema of its arguments.</summary>
public sealed record ToolSpec(string Name, string Description, string ParametersSchema);

/// <summary>
/// The companion's only source of match facts: read-only questions over the live
/// <see cref="MatchSnapshot"/>, answered as JSON. Places and team names come from the listener's
/// phrase bank, so an answer uses the same words as the commentary. Bad arguments get an error
/// the model can act on, never an exception.
/// </summary>
public sealed class MatchTools(Func<MatchSnapshot?> snapshot, string lang)
{
    public const int MaxEvents = 10;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Realizer _words = new(PhraseBankLoader.Get(lang));

    public static IReadOnlyList<ToolSpec> Specs { get; } =
    [
        new("get_score_and_time",
            "The score with every goal (scorer, team and minute), the match clock and whether the ball is in play. Use for any question about the score, the goals or the time.",
            """{"type":"object","properties":{}}"""),
        new("get_ball_and_possession",
            "Where the ball is now, which team and player have it, which way each team is attacking, and any restart about to be taken.",
            """{"type":"object","properties":{}}"""),
        new("get_recent_events",
            "The latest key events, newest first: goals, shots, cards, substitutions, fouls, offsides, corners, and the start and end of each half. To find goals, cards or substitutions, filter with kinds: unfiltered, the newest few events are usually fouls and corners.",
            """{"type":"object","properties":{"count":{"type":"integer","minimum":1,"maximum":10,"description":"How many events, default 5."},"kinds":{"type":"array","items":{"type":"string","enum":["goal","shot","card","substitution","foul","offside","corner","period"]},"description":"Only these kinds of event. Leave out for all kinds."}}}"""),
        new("get_match_stats",
            "Team statistics so far: possession, shots, shots on target, expected goals, passing, corners, fouls, cards and offsides.",
            """{"type":"object","properties":{}}"""),
        new("get_player",
            "A player by name, part of a name, or shirt number: team, shirt number, position, whether they are on the pitch, and their key events in this match.",
            """{"type":"object","properties":{"name":{"type":"string","description":"The player's name, part of it, or shirt number."}},"required":["name"]}"""),
    ];

    public string Invoke(string name, string? argumentsJson)
    {
        JsonElement args;
        try
        {
            args = string.IsNullOrWhiteSpace(argumentsJson) ? default : JsonDocument.Parse(argumentsJson).RootElement.Clone();
        }
        catch (JsonException)
        {
            return Error("The arguments were not valid JSON. Send an object matching the tool's schema.");
        }

        if (snapshot() is not { } s)
        {
            return Serialize(new { status = "The match hasn't kicked off yet." });
        }

        try
        {
            return name switch
            {
                "get_score_and_time" => Serialize(ScoreAndTime(s)),
                "get_ball_and_possession" => Serialize(BallAndPossession(s)),
                "get_recent_events" => RecentEvents(s, args),
                "get_match_stats" => Serialize(Stats(s)),
                "get_player" => Player(s, args),
                _ => Error($"There is no tool called '{name}'. The tools are: {string.Join(", ", Specs.Select(t => t.Name))}."),
            };
        }
        catch (Exception ex) when (ex is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            // A conversation must survive a bad call: the model gets told and can try another way.
            return Error("That question couldn't be answered from the match data. Try a different tool or simpler arguments.");
        }
    }

    private object ScoreAndTime(MatchSnapshot s)
    {
        var slots = new SlotFactory(s.Info);
        var (minute, added) = MatchClock.Minute(s.Frame.Period, s.Frame.MatchClockMs);
        return new
        {
            clock = s.Clock,
            minute,
            addedTime = added > 0 ? added : (int?)null,
            half = s.Frame.Period == 1 ? "first half" : "second half",
            status = s.FullTime ? "full time" : s.Ball.Status == BallStatus.InPlay ? "in play" : "ball out of play",
            home = new { team = s.Info.Home.Name, onAirName = _words.TeamName(slots.Team(s.Info.Home.TeamId)), goals = s.Ball.HomeScore },
            away = new { team = s.Info.Away.Name, onAirName = _words.TeamName(slots.Team(s.Info.Away.TeamId)), goals = s.Ball.AwayScore },
            scorers = s.KeyEvents
                .Where(e => e.Type == MatchEventType.Goal && e.TeamId is not null)
                .Select(e => new { clock = e.DisplayClock, team = _words.TeamName(slots.Team(e.TeamId!)), player = s.Info.FindPlayer(e.PlayerId)?.Name })
                .ToList(),
            spoken = $"{_words.Format(slots.Score(s.Ball))}, {_words.Format(slots.Clock(s.Frame.Period, s.Frame.MatchClockMs))}",
        };
    }

    private object BallAndPossession(MatchSnapshot s)
    {
        var slots = new SlotFactory(s.Info);
        var period = s.Frame.Period;
        var team = s.Ball.PossessionTeamId;
        var carrier = s.Info.FindPlayer(s.Ball.CarrierId);
        return new
        {
            clock = s.Clock,
            ballInPlay = s.Ball.Status == BallStatus.InPlay,
            inTheAir = s.Ball.InFlight ? true : (bool?)null,
            possession = team is null ? null : _words.TeamName(slots.Team(team)),
            player = carrier is null ? null : new { name = carrier.Name, number = carrier.Number, position = Position(carrier.Role) },
            where = team is null ? null : _words.Format(slots.Zone(s.Ball.Position, team, period)),
            restart = s.Ball.PendingRestart is { } restart ? Kind(restart) : null,
            attacking = new[] { s.Info.Home, s.Info.Away }.Select(t => new
            {
                team = _words.TeamName(slots.Team(t.TeamId)),
                towards = _words.Format(slots.AttackingSide(t.TeamId, period)),
            }),
            viewpoint = "Left and right are as seen from the main stand.",
        };
    }

    private string RecentEvents(MatchSnapshot s, JsonElement args)
    {
        var count = 5;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("count", out var c))
        {
            if (c.ValueKind != JsonValueKind.Number || !c.TryGetInt32(out count) || count < 1)
            {
                return Error("'count' must be a whole number from 1 to 10.");
            }
        }

        HashSet<string>? kinds = null;
        if (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("kinds", out var k) && k.ValueKind == JsonValueKind.Array)
        {
            kinds = k.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var events = s.KeyEvents.Where(Shown)
            .Where(e => kinds is null || kinds.Contains(Kind(e.Type)))
            .Reverse()
            .Take(Math.Min(count, MaxEvents))
            .Select(e => Describe(s, e))
            .ToList();
        return Serialize(new { clock = s.Clock, events });
    }

    private object Stats(MatchSnapshot s)
    {
        var slots = new SlotFactory(s.Info);
        return new
        {
            clock = s.Clock,
            teams = new[] { s.Info.Home, s.Info.Away }.Select(t =>
            {
                var x = s.Totals(t.TeamId);
                return new
                {
                    team = _words.TeamName(slots.Team(t.TeamId)),
                    possessionPercent = Math.Round(x.PossessionShare * 100),
                    shots = x.Shots,
                    shotsOnTarget = x.ShotsOnTarget,
                    expectedGoals = Math.Round(x.Xg, 1),
                    passesCompleted = x.PassesCompleted,
                    passAccuracyPercent = x.PassesAttempted == 0 ? 0 : Math.Round(100.0 * x.PassesCompleted / x.PassesAttempted),
                    corners = x.Corners,
                    fouls = x.Fouls,
                    yellowCards = x.YellowCards,
                    redCards = x.RedCards,
                    offsides = x.Offsides,
                };
            }),
        };
    }

    private string Player(MatchSnapshot s, JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty("name", out var n) || n.ValueKind != JsonValueKind.String || n.GetString() is not { Length: > 0 and <= 60 } query)
        {
            return Error("'name' is required: the player's name or part of it, up to 60 characters.");
        }

        var slots = new SlotFactory(s.Info);
        var playerQuery = query.Trim();
        var byShirtNumber = int.TryParse(playerQuery, NumberStyles.None, CultureInfo.InvariantCulture, out var shirtNumber);
        var matches = new[] { s.Info.Home, s.Info.Away }
            .SelectMany(t => t.Starters.Select(p => (Team: t, Player: p, Starter: true)).Concat(t.Bench.Select(p => (Team: t, Player: p, Starter: false))))
            .Where(x => byShirtNumber
                ? x.Player.Number == shirtNumber
                : x.Player.Name.Contains(playerQuery, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .Select(x =>
            {
                var events = s.KeyEvents.Where(Shown).Where(e => e.PlayerId == x.Player.PlayerId || e.ReplacedPlayerId == x.Player.PlayerId).ToList();
                var cameOn = events.Any(e => e.Type == MatchEventType.Substitution && e.PlayerId == x.Player.PlayerId);
                var wentOff = events.Any(e => e.Type == MatchEventType.Substitution && e.ReplacedPlayerId == x.Player.PlayerId);
                var sentOff = events.Any(e => e.Type == MatchEventType.Card && e.PlayerId == x.Player.PlayerId && e.Outcome is EventOutcome.Red or EventOutcome.SecondYellow);
                return new
                {
                    name = x.Player.Name,
                    team = _words.TeamName(slots.Team(x.Team.TeamId)),
                    number = x.Player.Number,
                    position = Position(x.Player.Role),
                    onThePitch = (x.Starter || cameOn) && !wentOff && !sentOff,
                    hasTheBall = s.Ball.CarrierId == x.Player.PlayerId ? true : (bool?)null,
                    events = events.Select(e => Describe(s, e)).ToList(),
                };
            })
            .ToList();

        return Serialize(new { clock = s.Clock, players = matches });
    }

    private object Describe(MatchSnapshot s, MatchEvent e)
    {
        var slots = new SlotFactory(s.Info);
        var player = s.Info.FindPlayer(e.PlayerId);
        return new
        {
            clock = e.DisplayClock,
            kind = Kind(e.Type),
            what = What(e),
            team = e.TeamId is null ? null : _words.TeamName(slots.Team(e.TeamId)),
            player = player?.Name,
            replaced = s.Info.FindPlayer(e.ReplacedPlayerId)?.Name,
            where = e.Location is { } at && e.TeamId is not null && e.Type is MatchEventType.Shot or MatchEventType.Goal or MatchEventType.Foul
                ? _words.Format(slots.Zone(at, e.TeamId, e.Period))
                : null,
        };
    }

    /// <summary>A scored shot is also a goal event; list the goal once.</summary>
    private static bool Shown(MatchEvent e) => !(e.Type == MatchEventType.Shot && e.Outcome == EventOutcome.Goal);

    private static string What(MatchEvent e) => (e.Type, e.Outcome) switch
    {
        (MatchEventType.PeriodStart, _) => e.Period == 1 ? "kick-off" : "second half under way",
        (MatchEventType.PeriodEnd, _) => e.Period == 1 ? "half time" : "full time",
        (MatchEventType.Goal, _) => "goal",
        (MatchEventType.Shot, EventOutcome.Saved) => "shot, saved",
        (MatchEventType.Shot, EventOutcome.OffTarget) => "shot, off target",
        (MatchEventType.Shot, EventOutcome.Blocked) => "shot, blocked",
        (MatchEventType.Shot, _) => "shot",
        (MatchEventType.Card, EventOutcome.Red) => "red card",
        (MatchEventType.Card, EventOutcome.SecondYellow) => "second yellow card, sent off",
        (MatchEventType.Card, _) => "yellow card",
        (MatchEventType.Substitution, _) => "substitution: player came on, replaced went off",
        (MatchEventType.Foul, _) => "foul committed",
        (MatchEventType.Offside, _) => "offside",
        (MatchEventType.Corner, _) => "corner",
        _ => e.Type.ToString(),
    };

    private static string Kind(MatchEventType type) => type switch
    {
        MatchEventType.PeriodStart or MatchEventType.PeriodEnd => "period",
        MatchEventType.ThrowIn => "throw-in",
        MatchEventType.GoalKick => "goal kick",
        MatchEventType.FreeKick => "free kick",
        MatchEventType.KickOff => "kick-off",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string Position(Role role) => role switch
    {
        Role.Goalkeeper => "goalkeeper",
        Role.CentreBack => "centre-back",
        Role.FullBack => "full-back",
        Role.DefensiveMidfielder => "defensive midfielder",
        Role.CentralMidfielder => "central midfielder",
        Role.Winger => "winger",
        Role.Striker => "striker",
        _ => role.ToString().ToLowerInvariant(),
    };

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);

    private static string Error(string message) => Serialize(new { error = message });
}
