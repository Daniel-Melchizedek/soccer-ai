using Vog.Contracts;
using Vog.Domain;

namespace Vog.Simulator;

/// <summary>
/// Deterministic, seeded football simulator. Each <see cref="Run"/> step advances 100 ms and yields a
/// 10 Hz tracking frame plus the match events and scene details that happened in that step.
/// The same <see cref="MatchSetup.Seed"/> always produces the same match.
/// </summary>
public sealed partial class MatchEngine
{
    public const int TickMs = 100;
    private const double Dt = TickMs / 1000.0;

    private readonly MatchSetup _setup;
    private readonly Rng _rng;
    private readonly SimTeam _home;
    private readonly SimTeam _away;
    private readonly SimBall _ball = new();
    private readonly List<MatchEvent> _tickEvents = [];
    private readonly List<SceneEvent> _tickScenes = [];

    private int _period;
    private long _clockMs;
    private long _periodEndMs;
    private long _frameId;
    private long _seq;
    private long _sceneSeq;
    private int _possessionId;
    private SimTeam? _possessionTeam;
    private long _lastPossessionChangeMs = long.MinValue / 2;
    private long _lastRestartMs = long.MinValue / 2;
    private bool _finished;

    public MatchEngine(MatchSetup setup)
    {
        _setup = setup;
        _rng = new Rng(setup.Seed);
        _home = CreateTeam(setup.Home, isHome: true);
        _away = CreateTeam(setup.Away, isHome: false);
        _home.Opponent = _away;
        _away.Opponent = _home;
        Info = new MatchInfo(setup.MatchId, setup.Competition, setup.Home.ToInfo(), setup.Away.ToInfo(), setup.Seed);
    }

    public MatchInfo Info { get; }

    public int HomeScore => _home.Score;

    public int AwayScore => _away.Score;

    public IEnumerable<MatchTick> Run()
    {
        StartPeriod(1);
        yield return Snapshot();

        while (!_finished)
        {
            _tickEvents.Clear();
            _tickScenes.Clear();
            _clockMs += TickMs;
            _frameId++;
            _ball.PrevPos = _ball.Pos;

            UpdateBall();
            if (_ball.Mode == BallMode.Owned)
            {
                UpdateCarrier();
            }

            MovePlayers();
            FollowOwner();
            CheckPeriodEnd();

            yield return Snapshot();
        }
    }

    private SimTeam CreateTeam(Club club, bool isHome)
    {
        var team = new SimTeam(club, isHome);
        for (var slot = 0; slot < club.Starters.Count; slot++)
        {
            team.Players.Add(new SimPlayer(club.Starters[slot], team, slot));
        }

        foreach (var benchPlayer in club.Bench.Where(p => p.Role != Role.Goalkeeper))
        {
            team.Bench.Enqueue(benchPlayer);
        }

        // Three substitution windows, taken at the first dead ball after each time.
        foreach (var minute in new[] { _rng.Uniform(56, 64), _rng.Uniform(66, 74), _rng.Uniform(76, 84) })
        {
            team.SubstitutionTimesMs.Add((long)(minute * 60_000));
        }

        return team;
    }

    private void StartPeriod(int period)
    {
        _period = period;
        _clockMs = MatchClock.PeriodStartMs(period);
        var addedMs = (long)(period == 1 ? _rng.Uniform(1, 4) : _rng.Uniform(2, 6)) * 60_000 + _rng.NextInt(0, 50) * 1000;
        _periodEndMs = _clockMs + MatchClock.HalfMs + addedMs;

        Emit(MatchEventType.PeriodStart, null, null, null);

        var kickingTeam = period == 1 ? _home : _away;
        foreach (var team in new[] { _home, _away })
        {
            foreach (var p in team.OnPitch)
            {
                p.Pos = KickOffPosition(p);
                p.Vx = p.Vy = 0;
            }
        }

        // The first kick-off leaves time for the pre-match description (kits, weather).
        SetDeadBall(MatchEventType.KickOff, kickingTeam, Pitch.Centre, delayMs: period == 1 ? 12000 : 4000);

        if (period == 1)
        {
            foreach (var team in new[] { _home, _away })
            {
                Scene(SceneKind.Kit, [team.Id], new Dictionary<string, string> { ["team"] = team.Id, ["colour"] = team.Club.KitColour });
            }

            var weather = _rng.Pick(new[] { "clear_evening", "light_rain", "bright_sunshine", "floodlit_night", "breezy" });
            Scene(SceneKind.Weather, [], new Dictionary<string, string> { ["type"] = weather });
        }
    }

    private void CheckPeriodEnd()
    {
        if (_clockMs < _periodEndMs || _ball.Mode == BallMode.InFlight)
        {
            return;
        }

        Emit(MatchEventType.PeriodEnd, null, null, _ball.Pos);
        if (_period == 1)
        {
            StartPeriod(2);
        }
        else
        {
            _finished = true;
        }
    }

    private bool AttacksRight(SimTeam team) => team.AttacksRight(_period);

    private SimTeam TeamById(string id) => id == _home.Id ? _home : _away;

    private MatchEvent Emit(
        MatchEventType type,
        SimTeam? team,
        SimPlayer? player,
        PitchPoint? location,
        PitchPoint? end = null,
        EventOutcome outcome = EventOutcome.None,
        SimPlayer? receiver = null,
        SimPlayer? opponent = null,
        PassHeight? height = null,
        bool underPressure = false,
        string? replacedPlayerId = null,
        string? relatedEventId = null)
    {
        _seq++;
        var e = new MatchEvent(
            EventId: $"{_setup.MatchId}-e{_seq:D5}",
            MatchId: _setup.MatchId,
            Seq: _seq,
            Period: _period,
            MatchClockMs: _clockMs,
            DisplayClock: MatchClock.Display(_period, _clockMs),
            Type: type,
            TeamId: team?.Id,
            PlayerId: player?.Id,
            Location: location,
            EndLocation: end,
            Outcome: outcome,
            ReceiverId: receiver?.Id,
            OpponentId: opponent?.Id,
            ReplacedPlayerId: replacedPlayerId,
            Height: height,
            UnderPressure: underPressure,
            PossessionId: _possessionId,
            PossessionTeamId: _possessionTeam?.Id,
            Phase: CurrentPhase(type),
            RelatedEventId: relatedEventId);
        _tickEvents.Add(e);
        return e;
    }

    private void Scene(SceneKind kind, IReadOnlyList<string> actors, IReadOnlyDictionary<string, string> attrs, string? causeEventId = null)
    {
        _sceneSeq++;
        _tickScenes.Add(new SceneEvent($"{_setup.MatchId}-s{_sceneSeq:D4}", _clockMs, kind, actors, attrs, causeEventId));
    }

    private PlayPhase CurrentPhase(MatchEventType type)
    {
        if (type is MatchEventType.KickOff or MatchEventType.ThrowIn or MatchEventType.Corner or MatchEventType.GoalKick or MatchEventType.FreeKick
            || _clockMs - _lastRestartMs < 6000)
        {
            return PlayPhase.SetPiece;
        }

        if (_ball.Mode == BallMode.Dead)
        {
            return PlayPhase.Dead;
        }

        if (_clockMs - _lastPossessionChangeMs < 6000)
        {
            return PlayPhase.Transition;
        }

        if (_possessionTeam is null)
        {
            return PlayPhase.Progression;
        }

        return Pitch.ZoneFor(_ball.Pos, AttacksRight(_possessionTeam)).Third switch
        {
            PitchThird.Defensive => PlayPhase.BuildUp,
            PitchThird.Middle => PlayPhase.Progression,
            _ => PlayPhase.FinalThird,
        };
    }

    private void SetPossession(SimTeam team, SimPlayer? player, SimPlayer? previousOwner)
    {
        if (_possessionTeam == team)
        {
            return;
        }

        var first = _possessionTeam is null;
        _possessionTeam = team;
        _possessionId++;
        if (!first)
        {
            _lastPossessionChangeMs = _clockMs;
            Emit(MatchEventType.PossessionChange, team, player, player?.Pos ?? _ball.Pos, opponent: previousOwner);
        }
    }

    private MatchTick Snapshot()
    {
        var players = new List<PlayerFrame>(22);
        foreach (var team in new[] { _home, _away })
        {
            foreach (var p in team.OnPitch)
            {
                players.Add(new PlayerFrame(p.Id, team.Id, p.Profile.Number, Math.Round(p.Pos.X, 2), Math.Round(p.Pos.Y, 2), Math.Round(p.Vx, 2), Math.Round(p.Vy, 2)));
            }
        }

        var vx = (_ball.Pos.X - _ball.PrevPos.X) / Dt;
        var vy = (_ball.Pos.Y - _ball.PrevPos.Y) / Dt;
        var ball = new BallFrame(
            Math.Round(_ball.Pos.X, 2),
            Math.Round(_ball.Pos.Y, 2),
            Math.Round(_ball.Z, 2),
            Math.Round(vx, 2),
            Math.Round(vy, 2),
            _ball.Mode == BallMode.Owned ? _ball.Owner?.Id : null);

        var frame = new TrackingFrame(_setup.MatchId, _frameId, _period, _clockMs, ball, players);
        return new MatchTick(frame, _tickEvents.ToList(), _tickScenes.ToList());
    }
}
