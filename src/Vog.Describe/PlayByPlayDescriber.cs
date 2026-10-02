using Vog.Contracts;
using Vog.Domain;
using Vog.Stats;

namespace Vog.Describe;

/// <summary>
/// HOT-path describer: turns interpreted events, player tags and scene details into
/// language-neutral <see cref="UtteranceIntent"/>s. No LLM is involved, so it runs in microseconds
/// and can only say what the data supports.
/// </summary>
public sealed class PlayByPlayDescriber
{
    /// <summary>Scene types the phrase banks know. Anything else is not described ("scene lock").</summary>
    private static readonly IReadOnlySet<string> KnownWeather = new HashSet<string> { "clear_evening", "light_rain", "bright_sunshine", "floodlit_night", "breezy" };

    private static readonly IReadOnlySet<string> KnownCelebrations = new HashSet<string> { "knee_slide", "corner_flag_dance", "team_huddle", "arms_wide", "cupped_ear", "jump_and_punch" };

    private readonly SlotFactory _slots;
    private readonly MatchInfo _info;
    private readonly HashSet<int> _periodsKickedOff = [];
    private MatchEvent? _previous;
    private long _seq;

    public PlayByPlayDescriber(MatchInfo info)
    {
        _info = info;
        _slots = new SlotFactory(info);
    }

    public IReadOnlyList<UtteranceIntent> Describe(StatsUpdate update)
    {
        var pts = update.Frame.PtsMs;
        var output = new List<UtteranceIntent>();

        foreach (var enriched in update.Events)
        {
            // Time-stamp intents with when the event happened, not when stats released it,
            // so freshness and event-to-speech latency are measured honestly.
            var lag = update.Frame.MatchClockMs - enriched.Event.MatchClockMs;
            var eventPts = enriched.Event.Period == update.Frame.Period && lag is >= 0 and < 5000 ? pts - lag : pts;
            DescribeEvent(enriched, update.Ball, eventPts, output);
            _previous = enriched.Event;
        }

        foreach (var tag in update.Tags)
        {
            DescribeTag(tag, pts, output);
        }

        foreach (var scene in update.Scenes)
        {
            DescribeScene(scene, update.Frame.Period, pts, output);
        }

        return output;
    }

    private void DescribeEvent(EnrichedEvent enriched, BallState ball, long pts, List<UtteranceIntent> output)
    {
        var e = enriched.Event;
        var team = e.TeamId;

        switch (e.Type)
        {
            case MatchEventType.KickOff when team is not null && _periodsKickedOff.Add(e.Period):
                output.Add(Intent(IntentKind.DirectionOfPlay, e.Period == 1 ? "pbp.kickoff" : "pbp.second_half",
                    new() { ["team"] = _slots.Team(team), ["side"] = _slots.AttackingSide(team, e.Period) },
                    Priority.P1, pts, validMs: 20000, value: 0.9, energy: 0.6, earcon: "whistle", cause: e.EventId));
                break;

            case MatchEventType.Pass when team is not null && e.Outcome == EventOutcome.Complete && e.Location is { } from && e.EndLocation is { } to:
                if (_previous?.Type == MatchEventType.Corner)
                {
                    break; // "swings the corner in" already said it
                }

                var attackRight = _info.AttacksRight(team, e.Period);
                var key = e.Height == PassHeight.Lofted && Pitch.InAttackingBox(to, attackRight) ? "pbp.cross"
                    : from.DistanceTo(to) >= 30 ? "pbp.pass.long"
                    : "pbp.pass.short";
                var progressive = Pitch.ZoneFor(to, attackRight).Third == PitchThird.Attacking;
                output.Add(Intent(IntentKind.PbpPass, key,
                    new() { ["player"] = _slots.Player(e.PlayerId), ["receiver"] = _slots.Player(e.ReceiverId), ["zone"] = _slots.Zone(to, team, e.Period) },
                    Priority.P2, pts, validMs: key == "pbp.pass.short" ? 2500 : 3500, value: progressive ? 0.5 : 0.3,
                    mentionsLocation: true, supersede: "pbp", energy: 0.4, spatial: to, cause: e.EventId));
                break;

            case MatchEventType.Carry when team is not null && e.Location is { } carryFrom && e.EndLocation is { } carryTo && carryFrom.DistanceTo(carryTo) >= 6:
                output.Add(Intent(IntentKind.PbpCarry, "pbp.carry",
                    new() { ["player"] = _slots.Player(e.PlayerId), ["zone"] = _slots.Zone(carryTo, team, e.Period) },
                    Priority.P2, pts, validMs: 2500, value: 0.25, mentionsLocation: true, supersede: "pbp", energy: 0.45, spatial: carryTo, cause: e.EventId));
                break;

            case MatchEventType.Clearance:
                output.Add(Intent(IntentKind.PbpPass, "pbp.clearance",
                    new() { ["player"] = _slots.Player(e.PlayerId) },
                    Priority.P2, pts, validMs: 2500, value: 0.35, supersede: "pbp", energy: 0.5, spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.PossessionChange when team is not null && e.Location is { } where:
                var previous = _previous;
                if (previous?.Type is MatchEventType.Tackle or MatchEventType.Save or MatchEventType.KickOff or MatchEventType.ThrowIn
                    or MatchEventType.GoalKick or MatchEventType.FreeKick or MatchEventType.Corner)
                {
                    break; // already described, or the restart team simply has the ball
                }

                var intercepted = previous is { Type: MatchEventType.Pass, Outcome: EventOutcome.Incomplete, OpponentId: not null };
                output.Add(Intent(IntentKind.PossessionChange, intercepted ? "pbp.intercepted" : "pbp.possession",
                    new() { ["team"] = _slots.Team(team), ["player"] = _slots.Player(e.PlayerId), ["zone"] = _slots.Zone(where, team, e.Period) },
                    Priority.P1, pts, validMs: 4000, value: 0.6, mentionsLocation: true, supersede: "pbp", energy: 0.55,
                    earcon: PossessionEarcon(team), spatial: where, cause: e.EventId));
                break;

            case MatchEventType.Tackle when team is not null && e.Location is { } tackleAt:
                output.Add(Intent(IntentKind.PossessionChange, "pbp.tackle",
                    new() { ["player"] = _slots.Player(e.PlayerId), ["victim"] = _slots.Player(e.OpponentId), ["zone"] = _slots.Zone(tackleAt, team, e.Period) },
                    Priority.P1, pts, validMs: 4000, value: 0.65, mentionsLocation: true, supersede: "pbp", energy: 0.6,
                    earcon: PossessionEarcon(team), spatial: tackleAt, cause: e.EventId));
                break;

            case MatchEventType.Shot when team is not null && e.Location is { } shotFrom:
                var en = enriched.Enrichment;
                var shotSlots = new Dictionary<string, Slot> { ["player"] = _slots.Player(e.PlayerId) };
                string shotKey;
                if (e.Height == PassHeight.Lofted)
                {
                    shotKey = "pbp.header";
                }
                else
                {
                    shotSlots["distance"] = SlotFactory.Number(Math.Round(en.ShotDistanceM ?? 0), "m");
                    shotKey = "pbp.shot";
                }

                // The live call stays short so the outcome can follow at once;
                // the measured shot speed is an on-screen graphic, read at the next dead ball.
                output.Add(Intent(IntentKind.Shot, shotKey, shotSlots, Priority.P1, pts, validMs: 4000, value: 0.9,
                    supersede: "pbp", energy: 0.85, earcon: "shot", spatial: shotFrom, cause: e.EventId));

                if (en.ShotSpeedKmh is { } speed)
                {
                    output.Add(Intent(IntentKind.GraphicReadout, "graphic.shot_speed",
                        new() { ["speed"] = SlotFactory.Number(speed, "kmh") },
                        Priority.P3, pts, validMs: 60000, value: 0.5, energy: 0.4, cause: e.EventId));
                }

                if (e.Outcome == EventOutcome.Blocked)
                {
                    output.Add(Intent(IntentKind.ShotOutcome, "pbp.blocked", [], Priority.P1, pts, validMs: 7000, value: 0.95, energy: 0.8, cause: e.EventId));
                }

                break;

            case MatchEventType.Goal when team is not null:
                output.Add(Intent(IntentKind.Goal, "pbp.goal",
                    new() { ["player"] = _slots.Player(e.PlayerId), ["team"] = _slots.Team(team), ["score"] = _slots.Score(ball) },
                    Priority.P0, pts, validMs: 20000, value: 1.0, supersede: "pbp", energy: 1.0, earcon: "goal", spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.Save:
                output.Add(Intent(IntentKind.ShotOutcome, "pbp.saved",
                    new() { ["keeper"] = _slots.Player(e.PlayerId) },
                    Priority.P1, pts, validMs: 7000, value: 0.95, energy: 0.85, earcon: "saved", spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.BallOut when team is not null:
                var afterMiss = _previous is { Type: MatchEventType.Shot, Outcome: EventOutcome.OffTarget };
                if (afterMiss)
                {
                    output.Add(Intent(IntentKind.ShotOutcome, "pbp.wide", [], Priority.P1, pts, validMs: 7000, value: 0.95, energy: 0.7, earcon: "out_of_play", cause: e.EventId));
                }

                var restartKey = ball.PendingRestart switch
                {
                    MatchEventType.Corner => "pbp.out.corner",
                    MatchEventType.GoalKick => "pbp.out.goal_kick",
                    _ => "pbp.out.throw_in",
                };
                output.Add(Intent(IntentKind.SetPiece, restartKey,
                    new() { ["team"] = _slots.Team(_slots.OpponentId(team)) },
                    restartKey == "pbp.out.corner" ? Priority.P1 : Priority.P2, pts, validMs: 8000,
                    value: restartKey == "pbp.out.corner" ? 0.6 : 0.4, energy: 0.4,
                    earcon: afterMiss ? null : "out_of_play", spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.Corner:
                output.Add(Intent(IntentKind.SetPiece, "pbp.corner.taken",
                    new() { ["player"] = _slots.Player(e.PlayerId) },
                    Priority.P1, pts, validMs: 4000, value: 0.6, energy: 0.6, spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.Foul when team is not null && e.Location is { } foulAt:
                var victimTeam = _slots.OpponentId(team);
                output.Add(Intent(IntentKind.Foul, "pbp.foul",
                    new()
                    {
                        ["player"] = _slots.Player(e.PlayerId),
                        ["victim"] = _slots.Player(e.OpponentId),
                        ["team"] = _slots.Team(victimTeam),
                        ["zone"] = _slots.Zone(foulAt, victimTeam, e.Period),
                    },
                    Priority.P1, pts, validMs: 8000, value: 0.6, energy: 0.5, earcon: "whistle", spatial: foulAt, cause: e.EventId));
                break;

            case MatchEventType.Card when team is not null:
                var (cardKey, cardPriority, cardEarcon) = e.Outcome switch
                {
                    EventOutcome.Red => ("pbp.card.red", Priority.P0, "card.red"),
                    EventOutcome.SecondYellow => ("pbp.card.second_yellow", Priority.P0, "card.red"),
                    _ => ("pbp.card.yellow", Priority.P1, "card.yellow"),
                };
                output.Add(Intent(IntentKind.Card, cardKey,
                    new() { ["player"] = _slots.Player(e.PlayerId), ["team"] = _slots.Team(team) },
                    cardPriority, pts, validMs: 30000, value: 0.8, energy: 0.6, earcon: cardEarcon, cause: e.EventId));
                break;

            case MatchEventType.Offside when team is not null:
                output.Add(Intent(IntentKind.Offside, "pbp.offside",
                    new() { ["player"] = _slots.Player(e.PlayerId), ["team"] = _slots.Team(_slots.OpponentId(team)) },
                    Priority.P1, pts, validMs: 6000, value: 0.55, energy: 0.45, earcon: "whistle", spatial: e.Location, cause: e.EventId));
                break;

            case MatchEventType.Substitution when team is not null:
                output.Add(Intent(IntentKind.Substitution, "pbp.sub",
                    new() { ["team"] = _slots.Team(team), ["player"] = _slots.Player(e.PlayerId), ["replaced"] = _slots.Player(e.ReplacedPlayerId) },
                    Priority.P1, pts, validMs: 40000, value: 0.5, energy: 0.4, cause: e.EventId));
                break;

            case MatchEventType.PeriodEnd:
                output.Add(Intent(IntentKind.PeriodChange, e.Period == 1 ? "pbp.halftime" : "pbp.fulltime",
                    new() { ["score"] = _slots.Score(ball) },
                    Priority.P0, pts, validMs: 60000, value: 1.0, energy: 0.7, earcon: "whistle", cause: e.EventId));
                break;
        }
    }

    private void DescribeTag(PlayerTag tag, long pts, List<UtteranceIntent> output)
    {
        switch (tag.Kind)
        {
            case PlayerTagKind.TopSpeed:
                output.Add(Intent(IntentKind.GraphicReadout, "graphic.top_speed",
                    new() { ["player"] = _slots.Player(tag.PlayerId), ["speed"] = SlotFactory.Number(tag.Value, "kmh") },
                    Priority.P3, pts, validMs: 60000, value: 0.4, energy: 0.4));
                break;

            case PlayerTagKind.DistanceMilestone when tag.Value >= 10:
                output.Add(Intent(IntentKind.GraphicReadout, "graphic.distance",
                    new() { ["player"] = _slots.Player(tag.PlayerId), ["distance"] = SlotFactory.Number(tag.Value, "km") },
                    Priority.P4, pts, validMs: 60000, value: 0.3, energy: 0.3));
                break;
        }
    }

    private void DescribeScene(SceneEvent scene, int period, long pts, List<UtteranceIntent> output)
    {
        scene.Attrs.TryGetValue("type", out var type);
        switch (scene.Kind)
        {
            case SceneKind.Kit when scene.Attrs.TryGetValue("team", out var teamId):
                output.Add(Intent(IntentKind.Scene, "scene.kit", new() { ["team"] = _slots.Team(teamId) },
                    Priority.P2, pts, validMs: 30000, value: 0.5, energy: 0.3));
                break;

            case SceneKind.Weather when type is not null && KnownWeather.Contains(type):
                output.Add(Intent(IntentKind.Scene, $"scene.weather.{type}", [], Priority.P3, pts, validMs: 30000, value: 0.3, energy: 0.3));
                break;

            case SceneKind.Celebration when type is not null && KnownCelebrations.Contains(type) && scene.Attrs.TryGetValue("player", out var scorer):
                output.Add(Intent(IntentKind.Scene, $"scene.celebration.{type}", new() { ["player"] = _slots.Player(scorer) },
                    Priority.P2, pts, validMs: 30000, value: 0.7, energy: 0.8));
                break;

            case SceneKind.BodyLanguage when type == "hands_on_head" && scene.Attrs.TryGetValue("player", out var player):
                output.Add(Intent(IntentKind.Scene, "scene.body.hands_on_head", new() { ["player"] = _slots.Player(player) },
                    Priority.P3, pts, validMs: 8000, value: 0.4, energy: 0.4));
                break;

            case SceneKind.Crowd when scene.Attrs.TryGetValue("level", out var level) && level is "roar" or "gasp":
                output.Add(Intent(IntentKind.Scene, $"scene.crowd.{level}", [], Priority.P3, pts, validMs: 6000, value: 0.3, energy: 0.7));
                break;

            case SceneKind.Var when type == "check_complete":
                output.Add(Intent(IntentKind.Scene, "scene.var.check_complete", [], Priority.P2, pts, validMs: 40000, value: 0.6, energy: 0.5));
                break;
        }
    }

    private string PossessionEarcon(string teamId) => teamId == _info.Home.TeamId ? "possession.home" : "possession.away";

    private UtteranceIntent Intent(
        IntentKind kind,
        string key,
        Dictionary<string, Slot> slots,
        Priority priority,
        long pts,
        long validMs,
        double value,
        bool mentionsLocation = false,
        string? supersede = null,
        double energy = 0.5,
        string? earcon = null,
        PitchPoint? spatial = null,
        string? cause = null) =>
        new(
            IntentId: $"{_info.MatchId}-i{++_seq:D6}",
            MatchId: _info.MatchId,
            Kind: kind,
            TemplateKey: key,
            Slots: slots,
            Priority: priority,
            Value: value,
            PtsMs: pts,
            ValidUntilMs: pts + validMs,
            MentionsBallLocation: mentionsLocation,
            SupersedeKey: supersede,
            Interruptible: priority != Priority.P0,
            Energy: energy,
            EarconId: earcon,
            Spatial: spatial,
            Source: IntentSource.Template,
            CauseEventId: cause);
}
