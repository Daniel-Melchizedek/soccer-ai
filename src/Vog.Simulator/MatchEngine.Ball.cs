using Vog.Contracts;
using Vog.Domain;

namespace Vog.Simulator;

public sealed partial class MatchEngine
{
    private static readonly string[] Celebrations =
        ["knee_slide", "corner_flag_dance", "team_huddle", "arms_wide", "cupped_ear", "jump_and_punch"];

    private MatchEventType? _restartInProgress;

    private void UpdateBall()
    {
        switch (_ball.Mode)
        {
            case BallMode.InFlight:
                var span = Math.Max(1, _ball.T1 - _ball.T0);
                var t = Math.Clamp((double)(_clockMs - _ball.T0) / span, 0, 1);
                _ball.Pos = PitchPoint.Lerp(_ball.From, _ball.To, t);
                _ball.Z = _ball.PeakZ * 4 * t * (1 - t);
                if (_clockMs >= _ball.T1)
                {
                    Arrive();
                }

                break;

            case BallMode.Dead:
                _ball.Z = 0;
                TrySubstitutions();
                if (_clockMs >= _ball.RestartAtMs)
                {
                    TakeRestart();
                }

                break;
        }
    }

    private void FollowOwner()
    {
        if (_ball.Mode != BallMode.Owned || _ball.Owner is null)
        {
            return;
        }

        var o = _ball.Owner;
        var speed = Math.Sqrt(o.Vx * o.Vx + o.Vy * o.Vy);
        var lead = speed > 0.3 ? 0.7 / speed : 0;
        _ball.Pos = Pitch.Clamp(new PitchPoint(o.Pos.X + o.Vx * lead, o.Pos.Y + o.Vy * lead));
        _ball.Z = 0;
    }

    private void SetDeadBall(MatchEventType restart, SimTeam team, PitchPoint spot, long delayMs)
    {
        _ball.Mode = BallMode.Dead;
        _ball.Owner = null;
        _ball.Target = null;
        _ball.RestartType = restart;
        _ball.RestartTeam = team;
        _ball.RestartSpot = spot;
        _ball.RestartAtMs = _clockMs + delayMs;
        _ball.Pos = spot;
        _ball.Z = 0;
    }

    private void TakeRestart()
    {
        var team = _ball.RestartTeam!;
        var spot = _ball.RestartSpot;
        var taker = RestartTaker(_ball.RestartType, team, spot);

        taker.Pos = spot;
        taker.Vx = taker.Vy = 0;
        _ball.Mode = BallMode.Owned;
        _ball.Owner = taker;
        _ball.Pos = spot;
        _ball.LastTouch = team;
        _lastRestartMs = _clockMs;
        _restartInProgress = _ball.RestartType;

        Emit(_ball.RestartType, team, taker, spot);
        SetPossession(team, taker, null);
        _decisionAtMs = _clockMs + (_ball.RestartType == MatchEventType.KickOff ? 300 : 600);
        _carryUntilMs = 0;
    }

    private SimPlayer RestartTaker(MatchEventType type, SimTeam team, PitchPoint spot)
    {
        var onPitch = team.OnPitch.ToList();
        return type switch
        {
            MatchEventType.KickOff => onPitch.FirstOrDefault(p => p.Profile.Role == Role.Striker) ?? onPitch.First(p => !p.IsGoalkeeper),
            MatchEventType.GoalKick => onPitch.FirstOrDefault(p => p.IsGoalkeeper) ?? onPitch.First(),
            _ => onPitch.Where(p => !p.IsGoalkeeper).MinBy(p => p.Pos.DistanceTo(spot))!,
        };
    }

    private void Arrive()
    {
        switch (_ball.Kind)
        {
            case FlightKind.Pass:
            case FlightKind.Loose:
                Receive(_ball.Target!);
                break;

            case FlightKind.Out:
                BallOut(_ball.To);
                break;

            case FlightKind.Shot:
                ResolveShot();
                break;
        }
    }

    private void Receive(SimPlayer receiver)
    {
        var previous = _ball.Kicker;
        _ball.Mode = BallMode.Owned;
        _ball.Owner = receiver;
        _ball.Pos = receiver.Pos;
        _ball.Z = 0;
        _ball.LastTouch = receiver.Team;
        SetPossession(receiver.Team, receiver, previous);

        var underPressure = NearestOpponentDistance(receiver) < 3;
        _decisionAtMs = _clockMs + DecisionDelayMs(receiver, underPressure);
        _carryUntilMs = 0;
        _restartInProgress = null;

        // A lofted ball into the box is often met first time.
        _forceHeader = _ball.LoftedCross
            && Pitch.InAttackingBox(receiver.Pos, AttacksRight(receiver.Team))
            && !receiver.IsGoalkeeper
            && _rng.Chance(0.45);
        if (_forceHeader)
        {
            _decisionAtMs = _clockMs;
        }
    }

    private void BallOut(PitchPoint where)
    {
        var lastTouch = _ball.LastTouch ?? _possessionTeam ?? _home;
        Emit(MatchEventType.BallOut, lastTouch, _ball.Kicker, Pitch.Clamp(where));

        if (where.Y < 0 || where.Y > Pitch.Width)
        {
            var spot = new PitchPoint(Math.Clamp(where.X, 1, Pitch.Length - 1), where.Y < 0 ? 0 : Pitch.Width);
            SetDeadBall(MatchEventType.ThrowIn, lastTouch.Opponent, spot, (long)_rng.Uniform(12000, 22000));
            return;
        }

        // Over a goal line: which team defends this end?
        var rightEnd = where.X > Pitch.Length / 2;
        var defending = AttacksRight(_home) == rightEnd ? _away : _home;
        if (lastTouch == defending)
        {
            var corner = new PitchPoint(rightEnd ? Pitch.Length : 0, where.Y < Pitch.Width / 2 ? 0 : Pitch.Width);
            SetDeadBall(MatchEventType.Corner, defending.Opponent, corner, (long)_rng.Uniform(26000, 38000));
        }
        else
        {
            var spot = new PitchPoint(rightEnd ? Pitch.Length - Pitch.SixYardDepth : Pitch.SixYardDepth, Pitch.Width / 2 + _rng.Uniform(-6, 6));
            SetDeadBall(MatchEventType.GoalKick, defending, spot, (long)_rng.Uniform(20000, 32000));
        }
    }

    private void ResolveShot()
    {
        var shooter = _ball.Kicker!;
        var attacking = shooter.Team;
        var defending = attacking.Opponent;
        var keeper = defending.OnPitch.FirstOrDefault(p => p.IsGoalkeeper);

        switch (_ball.Shot)
        {
            case ShotResult.Goal:
                attacking.Score++;
                var goal = Emit(MatchEventType.Goal, attacking, shooter, _ball.To, outcome: EventOutcome.Goal);
                Scene(SceneKind.Celebration, [shooter.Id], new Dictionary<string, string> { ["type"] = _rng.Pick(Celebrations), ["player"] = shooter.Id }, goal.EventId);
                Scene(SceneKind.Crowd, [], new Dictionary<string, string> { ["level"] = "roar", ["side"] = attacking.IsHome ? "home" : "away" }, goal.EventId);
                if (_rng.Chance(0.2))
                {
                    Scene(SceneKind.Var, [], new Dictionary<string, string> { ["type"] = "check_complete", ["decision"] = "goal_stands" }, goal.EventId);
                }

                foreach (var p in _home.OnPitch.Concat(_away.OnPitch))
                {
                    p.Pos = KickOffPosition(p);
                    p.Vx = p.Vy = 0;
                }

                SetDeadBall(MatchEventType.KickOff, defending, Pitch.Centre, (long)_rng.Uniform(55000, 80000));
                break;

            case ShotResult.Saved when keeper is not null:
                var save = Emit(MatchEventType.Save, defending, keeper, _ball.To, outcome: EventOutcome.Saved, opponent: shooter);
                if (_ball.ShotXg > 0.18)
                {
                    Scene(SceneKind.BodyLanguage, [shooter.Id], new Dictionary<string, string> { ["type"] = "hands_on_head", ["player"] = shooter.Id }, save.EventId);
                }

                if (_rng.Chance(0.4))
                {
                    // Parried behind: the keeper's touch puts it out, so it's a corner.
                    var rightEnd = _ball.To.X > Pitch.Length / 2;
                    var corner = new PitchPoint(rightEnd ? Pitch.Length : 0, _rng.Chance(0.5) ? 0 : Pitch.Width);
                    Emit(MatchEventType.BallOut, defending, keeper, new PitchPoint(corner.X, Pitch.Width / 2 + (corner.Y > 0 ? 6 : -6)));
                    SetDeadBall(MatchEventType.Corner, attacking, corner, (long)_rng.Uniform(26000, 38000));
                }
                else
                {
                    keeper.Pos = _ball.To.MoveTowards(Pitch.Centre, 1.5);
                    _ball.Kicker = shooter;
                    Receive(keeper);
                }

                break;

            case ShotResult.Blocked:
                var landing = Pitch.Clamp(new PitchPoint(_ball.To.X + _rng.Uniform(-8, 8), _ball.To.Y + _rng.Uniform(-8, 8)), -1);
                var nearest = _home.OnPitch.Concat(_away.OnPitch).Where(p => !p.IsGoalkeeper).MinBy(p => p.Pos.DistanceTo(landing))!;
                StartFlight(FlightKind.Loose, _ball.To, landing, speed: 12, nearest, _ball.Kicker, peakZ: 0.5);
                break;

            default:
                var miss = _ball.To;
                if (_ball.ShotXg > 0.18)
                {
                    Scene(SceneKind.BodyLanguage, [shooter.Id], new Dictionary<string, string> { ["type"] = "hands_on_head", ["player"] = shooter.Id });
                    Scene(SceneKind.Crowd, [], new Dictionary<string, string> { ["level"] = "gasp" });
                }

                BallOut(miss);
                break;
        }
    }

    private void StartFlight(FlightKind kind, PitchPoint from, PitchPoint to, double speed, SimPlayer? target, SimPlayer? kicker, double peakZ)
    {
        _ball.Mode = BallMode.InFlight;
        _ball.Owner = null;
        _ball.Kind = kind;
        _ball.From = from;
        _ball.To = to;
        _ball.T0 = _clockMs;
        _ball.T1 = _clockMs + Math.Max(200, (long)(from.DistanceTo(to) / Math.Max(1, speed) * 1000));
        _ball.Target = target;
        _ball.Kicker = kicker;
        _ball.PeakZ = peakZ;
        if (kicker is not null)
        {
            _ball.LastTouch = kicker.Team;
        }

        if (target is not null)
        {
            target.Target = to;
        }
    }

    private void TrySubstitutions()
    {
        foreach (var team in new[] { _home, _away })
        {
            if (team.SubstitutionTimesMs.Count == 0 || _clockMs < team.SubstitutionTimesMs[0] || team.Bench.Count == 0)
            {
                continue;
            }

            team.SubstitutionTimesMs.RemoveAt(0);
            var off = team.OnPitch.Where(p => !p.IsGoalkeeper).MaxBy(p => p.DistanceRun * (1.2 - p.Profile.Stamina));
            if (off is null)
            {
                continue;
            }

            var on = team.Bench.Dequeue();
            var offId = off.Id;
            var offNumber = off.Profile.Number;
            off.Profile = on;
            off.DistanceRun = 0;
            off.Yellows = 0;

            var touchline = new PitchPoint(Pitch.Length / 2, 0);
            Emit(MatchEventType.Substitution, team, off, touchline, replacedPlayerId: offId);
            Scene(SceneKind.SubBoard, [on.PlayerId, offId], new Dictionary<string, string>
            {
                ["team"] = team.Id,
                ["number_off"] = offNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["number_on"] = on.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
            _ball.RestartAtMs = Math.Max(_ball.RestartAtMs, _clockMs + 25000);
        }
    }
}
