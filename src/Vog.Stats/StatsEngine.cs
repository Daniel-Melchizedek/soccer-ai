using Vog.Contracts;
using Vog.Domain;
using Vog.Domain.Models;

namespace Vog.Stats;

/// <summary>
/// Stage (b), interpret: turns raw events and 10 Hz tracking into metrics, player tags and the live
/// ball state. Passes and shots are held for up to <see cref="SpeedSampleFrames"/> frames while ball
/// speed is measured from tracking; events are always released in their original order.
/// </summary>
public sealed class StatsEngine
{
    public const int SpeedSampleFrames = 3;

    /// <summary>League-average passing skill: difficulty describes the pass, not the passer.</summary>
    private const double AveragePasserSkill = 0.75;

    private readonly MatchInfo _info;
    private readonly Queue<PendingEvent> _pending = new();
    private TrackingFrame? _previous;

    private int _period = 1;
    private BallStatus _status = BallStatus.Dead;
    private MatchEventType? _pendingRestart = MatchEventType.KickOff;
    private long _lastRestartMs;
    private string? _possessionTeamId;
    private int _homeScore;
    private int _awayScore;

    public StatsEngine(MatchInfo info)
    {
        _info = info;
        Totals = new MatchStats(info.Home.TeamId, info.Away.TeamId);
    }

    public MatchStats Totals { get; }

    public PlayerMotionTracker Motion { get; } = new();

    public StatsUpdate Process(MatchTick tick)
    {
        var frame = tick.Frame;
        var tags = new List<PlayerTag>();

        var dt = _previous is null ? 0.1 : (frame.MatchClockMs - _previous.MatchClockMs) / 1000.0;
        Motion.Update(frame, dt is > 0 and <= 1 ? dt : 0.1, tags);

        foreach (var e in tick.Events)
        {
            _pending.Enqueue(Begin(e, frame));
        }

        foreach (var p in _pending)
        {
            p.Observe(frame);
        }

        var released = new List<EnrichedEvent>();
        while (_pending.Count > 0 && _pending.Peek().IsComplete)
        {
            var enriched = _pending.Dequeue().Finish();
            Apply(enriched);
            released.Add(enriched);
        }

        if (_status == BallStatus.InPlay && dt is > 0 and <= 1)
        {
            var ms = (long)(dt * 1000);
            Totals.InPlayMs += ms;
            if (_possessionTeamId is not null)
            {
                Totals.For(_possessionTeamId).PossessionMs += ms;
            }
        }

        _previous = frame;
        return new StatsUpdate(frame, BallStateFor(frame), released, tags, tick.Scenes);
    }

    /// <summary>Releases anything still pending (end of feed).</summary>
    public IReadOnlyList<EnrichedEvent> Flush()
    {
        var released = new List<EnrichedEvent>();
        while (_pending.Count > 0)
        {
            var enriched = _pending.Dequeue().Finish();
            Apply(enriched);
            released.Add(enriched);
        }

        return released;
    }

    private PendingEvent Begin(MatchEvent e, TrackingFrame frame)
    {
        switch (e.Type)
        {
            case MatchEventType.Pass when e.Location is { } from && e.EndLocation is { } end:
                var target = frame.Players.FirstOrDefault(p => p.PlayerId == e.ReceiverId)?.Position ?? end;
                var opponents = frame.Players.Where(p => p.TeamId != e.TeamId).Select(p => p.Position).ToList();
                var features = PassModel.Measure(from, target, opponents, e.Height == PassHeight.Lofted, AveragePasserSkill);
                var p = PassModel.SuccessProbability(features);
                var enrichment = new Enrichment(
                    PassDistanceM: Math.Round(from.DistanceTo(target), 1),
                    PSuccess: Math.Round(p, 3),
                    Difficulty: Math.Round(1 - p, 3),
                    DifficultyFactors: PassModel.Contributions(features).Where(c => Math.Abs(c.LogitDelta) > 0.05).ToList());
                return new PendingEvent(e, enrichment, frame.Ball.Position, measureSpeed: true, isShot: false);

            case MatchEventType.Shot when e.Location is { } shotFrom && e.TeamId is not null:
                var attackRight = _info.AttacksRight(e.TeamId, e.Period);
                var xg = XgModel.Probability(shotFrom, attackRight, header: e.Height == PassHeight.Lofted, e.UnderPressure);
                var shot = new Enrichment(
                    Xg: Math.Round(xg, 3),
                    ShotDistanceM: Math.Round(Pitch.DistanceToGoal(shotFrom, attackRight), 1));
                return new PendingEvent(e, shot, frame.Ball.Position, measureSpeed: true, isShot: true);

            default:
                return new PendingEvent(e, Enrichment.None, frame.Ball.Position, measureSpeed: false, isShot: false);
        }
    }

    private void Apply(EnrichedEvent enriched)
    {
        var e = enriched.Event;
        var team = e.TeamId is null ? null : Totals.For(e.TeamId);
        if (e.PossessionTeamId is not null)
        {
            _possessionTeamId = e.PossessionTeamId;
        }

        switch (e.Type)
        {
            case MatchEventType.PeriodStart:
                _period = e.Period;
                _status = BallStatus.Dead;
                _pendingRestart = MatchEventType.KickOff;
                break;
            case MatchEventType.PeriodEnd:
                _status = BallStatus.Dead;
                _pendingRestart = e.Period == 1 ? MatchEventType.KickOff : null;
                break;
            case MatchEventType.KickOff or MatchEventType.ThrowIn or MatchEventType.GoalKick or MatchEventType.FreeKick or MatchEventType.Corner:
                _status = BallStatus.InPlay;
                _pendingRestart = null;
                _lastRestartMs = e.MatchClockMs;
                _possessionTeamId = e.TeamId;
                if (team is not null)
                {
                    switch (e.Type)
                    {
                        case MatchEventType.Corner:
                            team.Corners++;
                            break;
                        case MatchEventType.ThrowIn:
                            team.ThrowIns++;
                            break;
                        case MatchEventType.GoalKick:
                            team.GoalKicks++;
                            break;
                    }
                }

                break;
            case MatchEventType.Pass when team is not null:
                team.PassesAttempted++;
                if (e.Outcome == EventOutcome.Complete)
                {
                    team.PassesCompleted++;
                }

                break;
            case MatchEventType.Shot when team is not null:
                team.Shots++;
                team.Xg += enriched.Enrichment.Xg ?? 0;
                if (e.Outcome is EventOutcome.Goal or EventOutcome.Saved)
                {
                    team.ShotsOnTarget++;
                }

                break;
            case MatchEventType.Goal when team is not null:
                team.Goals++;
                if (e.TeamId == _info.Home.TeamId)
                {
                    _homeScore++;
                }
                else
                {
                    _awayScore++;
                }

                _status = BallStatus.Dead;
                _pendingRestart = MatchEventType.KickOff;
                break;
            case MatchEventType.Foul when team is not null:
                team.Fouls++;
                _status = BallStatus.Dead;
                _pendingRestart = MatchEventType.FreeKick;
                break;
            case MatchEventType.Card when team is not null:
                if (e.Outcome == EventOutcome.Yellow)
                {
                    team.YellowCards++;
                }
                else
                {
                    team.RedCards++;
                }

                break;
            case MatchEventType.Offside when team is not null:
                team.Offsides++;
                _status = BallStatus.Dead;
                _pendingRestart = MatchEventType.FreeKick;
                break;
            case MatchEventType.Tackle when team is not null:
                team.Tackles++;
                break;
            case MatchEventType.BallOut:
                _status = BallStatus.Dead;
                _pendingRestart = InferRestart(e);
                break;
            case MatchEventType.PossessionChange:
                _possessionTeamId = e.TeamId;
                break;
        }
    }

    /// <summary>Applies the laws of the game to an out-of-play location to infer the restart.</summary>
    private MatchEventType InferRestart(MatchEvent e)
    {
        if (e.Location is not { } where || e.TeamId is null)
        {
            return MatchEventType.ThrowIn;
        }

        if (where.Y <= 0.01 || where.Y >= Pitch.Width - 0.01)
        {
            return MatchEventType.ThrowIn;
        }

        var rightEnd = where.X > Pitch.Length / 2;
        var homeDefendsRightEnd = !_info.AttacksRight(_info.Home.TeamId, e.Period);
        var defendingTeamId = homeDefendsRightEnd == rightEnd ? _info.Home.TeamId : _info.Away.TeamId;
        return e.TeamId == defendingTeamId ? MatchEventType.Corner : MatchEventType.GoalKick;
    }

    private BallState BallStateFor(TrackingFrame frame)
    {
        var carrier = frame.Ball.OwnerPlayerId;
        var teamId = carrier is not null
            ? frame.Players.FirstOrDefault(p => p.PlayerId == carrier)?.TeamId ?? _possessionTeamId
            : _possessionTeamId;
        var zone = teamId is null ? (Zone?)null : Pitch.ZoneFor(frame.Ball.Position, _info.AttacksRight(teamId, frame.Period));

        return new BallState(
            frame.Period,
            frame.MatchClockMs,
            _status,
            InFlight: _status == BallStatus.InPlay && carrier is null,
            PossessionTeamId: teamId,
            CarrierId: carrier,
            Position: frame.Ball.Position,
            Zone: zone,
            PendingRestart: _status == BallStatus.Dead ? _pendingRestart : null,
            LastRestartMs: _lastRestartMs,
            HomeScore: _homeScore,
            AwayScore: _awayScore);
    }

    private sealed class PendingEvent(MatchEvent e, Enrichment enrichment, PitchPoint ballAtEvent, bool measureSpeed, bool isShot)
    {
        private readonly PitchPoint _ballAtEvent = ballAtEvent;
        private int _frames;
        private PitchPoint _lastBall = ballAtEvent;
        private bool _stopped;

        public bool IsComplete => !measureSpeed || _frames >= SpeedSampleFrames || _stopped;

        public void Observe(TrackingFrame frame)
        {
            if (IsComplete || frame.MatchClockMs <= e.MatchClockMs)
            {
                return;
            }

            _frames++;
            _lastBall = frame.Ball.Position;

            // Received (or dead) early: measure over the frames we have.
            if (frame.Ball.OwnerPlayerId is not null)
            {
                _stopped = true;
            }
        }

        public EnrichedEvent Finish()
        {
            if (!measureSpeed || _frames == 0)
            {
                return new EnrichedEvent(e, enrichment);
            }

            var speedMs = _ballAtEvent.DistanceTo(_lastBall) / (_frames * 0.1);
            double? kmh = speedMs > 2 ? Math.Round(speedMs * 3.6, 1) : null;
            return new EnrichedEvent(e, isShot ? enrichment with { ShotSpeedKmh = kmh } : enrichment with { BallSpeedKmh = kmh });
        }
    }
}
