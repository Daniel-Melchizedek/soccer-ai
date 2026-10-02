using Vog.Contracts;
using Vog.Domain;
using Vog.Domain.Models;

namespace Vog.Simulator;

public sealed partial class MatchEngine
{
    /// <summary>Scales how readily players shoot relative to passing; tuned against the data card.</summary>
    private const double ShotBias = 0.58;

    /// <summary>Value of simply keeping the ball, so safe passes compete with risky forward ones.</summary>
    private const double PossessionValue = 0.035;

    /// <summary>Weight on the opponent's threat where the ball would be lost.</summary>
    private const double TurnoverCost = 1.2;

    private long _decisionAtMs;
    private long _carryUntilMs;
    private PitchPoint _carryTarget;
    private bool _forceHeader;
    private long _challengeCooldownUntilMs;

    private void UpdateCarrier()
    {
        var carrier = _ball.Owner!;
        var (challenger, distance) = NearestOpponent(carrier);

        if (challenger is not null && distance < 1.4 && _clockMs >= _challengeCooldownUntilMs && _restartInProgress is null)
        {
            var foulChance = 0.006 * (1.25 - challenger.Profile.Tackling);
            var tackleChance = 0.05 * challenger.Profile.Tackling * (1.15 - carrier.Profile.Passing * 0.3);

            // Defenders commit harder near their own goal.
            if (Pitch.ZoneFor(carrier.Pos, AttacksRight(carrier.Team)).Third == PitchThird.Attacking)
            {
                tackleChance *= 2.2;
            }

            var r = _rng.NextDouble();
            if (r < foulChance)
            {
                Foul(challenger, carrier);
                return;
            }

            if (r < foulChance + tackleChance)
            {
                Emit(MatchEventType.Tackle, challenger.Team, challenger, carrier.Pos, outcome: EventOutcome.Won, opponent: carrier);
                _challengeCooldownUntilMs = _clockMs + 1500;

                // A quarter of tackles knock the ball loose rather than winning it cleanly.
                if (_rng.Chance(0.35))
                {
                    Deflect(carrier.Pos, challenger);
                    return;
                }

                _ball.Kicker = carrier;
                Receive(challenger);
                return;
            }
        }

        if (_clockMs >= _decisionAtMs && _clockMs >= _carryUntilMs)
        {
            Decide(carrier, distance);
        }
    }

    private void Decide(SimPlayer carrier, double pressureDistance)
    {
        var team = carrier.Team;
        var attackRight = AttacksRight(team);
        var underPressure = pressureDistance < 3;
        var opponents = team.Opponent.OnPitch.Select(p => p.Pos).ToList();
        var restart = _restartInProgress;
        var passOnly = restart is not null and not MatchEventType.FreeKick;

        if (_forceHeader)
        {
            _forceHeader = false;
            Shoot(carrier, attackRight, underPressure, header: true);
            return;
        }

        // Under pressure deep in their own third, defenders often just clear their lines.
        if (!passOnly && !carrier.IsGoalkeeper && underPressure
            && Pitch.ToAttackFrame(carrier.Pos, attackRight).Depth < Pitch.Length / 3
            && _rng.Chance(0.45))
        {
            Clear(carrier, attackRight);
            _restartInProgress = null;
            return;
        }

        var options = new List<(double Ev, Action Execute)>();

        if (!passOnly && !carrier.IsGoalkeeper)
        {
            var distance = Pitch.DistanceToGoal(carrier.Pos, attackRight);
            var angle = Pitch.GoalAngle(carrier.Pos, attackRight);
            if (distance < 30 && angle > 0.15)
            {
                // Compressed in xG so speculative long shots still happen, as they do in real matches.
                var xg = XgModel.Probability(distance, angle, header: false, underPressure);
                var ev = Math.Pow(xg, 0.7) * (0.85 + carrier.Profile.Finishing * 0.6) * ShotBias;
                options.Add((ev, () => Shoot(carrier, attackRight, underPressure, header: false)));
            }
        }

        foreach (var mate in team.OnPitch)
        {
            if (mate == carrier)
            {
                continue;
            }

            var d = carrier.Pos.DistanceTo(mate.Pos);
            if (restart == MatchEventType.ThrowIn && d > 25)
            {
                continue;
            }

            var lofted = d > 30 || restart == MatchEventType.Corner;
            var features = PassModel.Measure(carrier.Pos, mate.Pos, opponents, lofted, carrier.Profile.Passing);
            var p = PassModel.SuccessProbability(features);
            var gain = ThreatModel.Value(mate.Pos, attackRight) + PossessionValue;
            var loss = ThreatModel.Value(PitchPoint.Lerp(carrier.Pos, mate.Pos, 0.5), !attackRight) * TurnoverCost + PossessionValue;
            var ev = p * gain - (1 - p) * loss;
            if (mate.IsGoalkeeper)
            {
                ev *= 0.4;
            }

            if (restart == MatchEventType.Corner && !Pitch.InAttackingBox(mate.Pos, attackRight))
            {
                ev *= 0.3;
            }

            var target = mate;
            options.Add((ev, () => Pass(carrier, target, features, p, lofted, underPressure)));
        }

        if (!passOnly && !carrier.IsGoalkeeper)
        {
            var forward = CarryTarget(carrier, attackRight);
            var crowd = team.Opponent.OnPitch.Count(o => o.Pos.DistanceTo(forward) < 6);
            var keep = Math.Max(0.3, 0.93 - (underPressure ? 0.22 : 0) - 0.12 * crowd);
            var ev = keep * ThreatModel.Value(forward, attackRight) - (1 - keep) * ThreatModel.Value(carrier.Pos, !attackRight) * 0.6;
            options.Add((ev, () => Carry(carrier, forward, underPressure)));
        }

        if (options.Count == 0)
        {
            _decisionAtMs = _clockMs + 500;
            return;
        }

        // Softmax over expected value; less skilled players make noisier choices.
        var temperature = 0.008 + 0.012 * (1 - carrier.Profile.Passing) + (underPressure ? 0.004 : 0);
        var best = options.Max(o => o.Ev);
        var weights = options.Select(o => Math.Exp((o.Ev - best) / temperature)).ToList();
        options[_rng.PickWeighted(weights)].Execute();
        _restartInProgress = null;
    }

    private void Pass(SimPlayer passer, SimPlayer receiver, PassModel.Features features, double pSuccess, bool lofted, bool underPressure)
    {
        var from = passer.Pos;
        var to = receiver.Pos;
        var distance = from.DistanceTo(to);
        var speed = lofted
            ? _rng.Uniform(15, 24)
            : Math.Clamp(9 + 0.32 * distance + _rng.Gaussian(0, 1.2), 9, 23);
        var success = _rng.Chance(pSuccess);
        var height = lofted ? PassHeight.Lofted : PassHeight.Ground;
        var attackRight = AttacksRight(passer.Team);

        if (success && IsOffside(passer, receiver, attackRight))
        {
            Emit(MatchEventType.Pass, passer.Team, passer, from, to, EventOutcome.Incomplete, receiver, height: height, underPressure: underPressure);
            Emit(MatchEventType.Offside, passer.Team, receiver, receiver.Pos);
            SetDeadBall(MatchEventType.FreeKick, passer.Team.Opponent, Pitch.Clamp(receiver.Pos, -1), (long)_rng.Uniform(18000, 30000));
            return;
        }

        if (success)
        {
            Emit(MatchEventType.Pass, passer.Team, passer, from, to, EventOutcome.Complete, receiver, height: height, underPressure: underPressure);
            _ball.LoftedCross = lofted;
            StartFlight(FlightKind.Pass, from, to, speed, receiver, passer, lofted ? Math.Min(12, 2 + distance * 0.12) : 0);
            return;
        }

        // Failed: either a defender cuts it out or the ball runs loose / out of play.
        var interceptor = passer.Team.Opponent.OnPitch
            .Select(o => (Player: o, Proj: PassModel.ProjectOntoSegment(from, to, o.Pos)))
            .Where(x => x.Proj.T is > 0.1 and < 1.15 && x.Proj.Perpendicular < 7)
            .OrderBy(x => x.Proj.Perpendicular + x.Proj.T * 2)
            .Select(x => x.Player)
            .FirstOrDefault();

        // An overhit cross sails over everyone, usually out behind the goal.
        var overhitCross = lofted && Pitch.InAttackingBox(to, attackRight) && _rng.Chance(0.4);

        if (interceptor is not null && !overhitCross)
        {
            var (t, _) = PassModel.ProjectOntoSegment(from, to, interceptor.Pos);
            var point = Pitch.Clamp(PitchPoint.Lerp(from, to, Math.Clamp(t, 0.2, 1.0)), -0.5);
            Emit(MatchEventType.Pass, passer.Team, passer, from, point, EventOutcome.Incomplete, receiver, interceptor, height, underPressure);
            _ball.LoftedCross = false;
            StartFlight(FlightKind.Pass, from, point, speed, interceptor, passer, lofted ? Math.Min(10, 2 + distance * 0.1) : 0);
            return;
        }

        var overshoot = _rng.Uniform(6, 18);
        var dir = distance > 0.1 ? new PitchPoint((to.X - from.X) / distance, (to.Y - from.Y) / distance) : new PitchPoint(1, 0);
        var landing = new PitchPoint(to.X + dir.X * overshoot + _rng.Gaussian(0, 3), to.Y + dir.Y * overshoot + _rng.Gaussian(0, 3));
        Emit(MatchEventType.Pass, passer.Team, passer, from, landing, EventOutcome.Incomplete, receiver, height: height, underPressure: underPressure);
        _ball.LoftedCross = false;

        if (!Pitch.IsInside(landing))
        {
            StartFlight(FlightKind.Out, from, landing, speed, null, passer, lofted ? 6 : 0);
            return;
        }

        var nearest = _home.OnPitch.Concat(_away.OnPitch).MinBy(p => p.Pos.DistanceTo(landing))!;
        StartFlight(FlightKind.Loose, from, landing, speed, nearest, passer, lofted ? 6 : 0);
    }

    private void Shoot(SimPlayer shooter, bool attackRight, bool underPressure, bool header)
    {
        var from = shooter.Pos;
        var xg = XgModel.Probability(from, attackRight, header, underPressure);
        var goalLineX = attackRight ? Pitch.Length : 0;
        var opponents = shooter.Team.Opponent.OnPitch.Where(p => !p.IsGoalkeeper).Select(p => p.Pos).ToList();
        var blockers = PassModel.Measure(from, Pitch.GoalCentre(attackRight), opponents, lofted: false, 0.75).LaneBlockers;
        var finishing = shooter.Profile.Finishing;

        ShotResult result;
        var r = _rng.NextDouble();
        var pBlock = Math.Min(0.35, 0.12 * blockers);
        if (r < pBlock)
        {
            result = ShotResult.Blocked;
        }
        else if (_rng.Chance(xg * (0.75 + 0.4 * finishing)))
        {
            result = ShotResult.Goal;
        }
        else
        {
            result = _rng.Chance(0.4) ? ShotResult.Saved : ShotResult.OffTarget;
        }

        var power = header ? _rng.Uniform(0.1, 0.4) : _rng.Uniform(0.25, 1.0);
        var speed = 15 + 18 * power;

        PitchPoint end = result switch
        {
            ShotResult.Goal => new PitchPoint(goalLineX, Pitch.Width / 2 + _rng.Uniform(-3.3, 3.3)),
            ShotResult.Saved => new PitchPoint(goalLineX + (attackRight ? -0.8 : 0.8), Pitch.Width / 2 + _rng.Uniform(-2.5, 2.5)),
            ShotResult.Blocked => PitchPoint.Lerp(from, Pitch.GoalCentre(attackRight), _rng.Uniform(0.2, 0.5)),
            _ => new PitchPoint(goalLineX + (attackRight ? 1.5 : -1.5), Pitch.Width / 2 + (_rng.Chance(0.5) ? 1 : -1) * _rng.Uniform(4.2, 9)),
        };

        var outcome = result switch
        {
            ShotResult.Goal => EventOutcome.Goal,
            ShotResult.Saved => EventOutcome.Saved,
            ShotResult.Blocked => EventOutcome.Blocked,
            _ => EventOutcome.OffTarget,
        };

        Emit(MatchEventType.Shot, shooter.Team, shooter, from, end, outcome, height: header ? PassHeight.Lofted : PassHeight.Ground, underPressure: underPressure);
        _ball.Shot = result;
        _ball.ShotXg = xg;
        _ball.LoftedCross = false;
        StartFlight(FlightKind.Shot, from, end, speed, null, shooter, header ? 1.5 : _rng.Uniform(0.2, 2.2));
    }

    /// <summary>A hoofed clearance towards the nearer flank; a good share go out for a throw-in.</summary>
    private void Clear(SimPlayer defender, bool attackRight)
    {
        var from = defender.Pos;
        var (_, fromLeft) = Pitch.ToAttackFrame(from, attackRight);
        var lateral = fromLeft < Pitch.Width / 2 ? _rng.Uniform(-6, 10) : Pitch.Width - _rng.Uniform(-6, 10);
        var landing = Pitch.FromAttackFrame(_rng.Uniform(45, 75), lateral, attackRight);
        Emit(MatchEventType.Clearance, defender.Team, defender, from, landing, underPressure: true);
        _ball.LoftedCross = false;

        var speed = _rng.Uniform(20, 27);
        if (!Pitch.IsInside(landing))
        {
            StartFlight(FlightKind.Out, from, landing, speed, null, defender, peakZ: 10);
            return;
        }

        var nearest = _home.OnPitch.Concat(_away.OnPitch).MinBy(p => p.Pos.DistanceTo(landing))!;
        StartFlight(FlightKind.Loose, from, landing, speed, nearest, defender, peakZ: 10);
    }

    /// <summary>The ball squirts away from a challenge: out of play, or loose to whoever is nearest.</summary>
    private void Deflect(PitchPoint from, SimPlayer lastTouch)
    {
        var angle = _rng.Uniform(0, 2 * Math.PI);

        // Near a touchline, challenges tend to push the ball over it.
        var nearBottom = from.Y < 12;
        var nearTop = from.Y > Pitch.Width - 12;
        if ((nearBottom || nearTop) && _rng.Chance(0.6))
        {
            angle = (nearBottom ? -Math.PI / 2 : Math.PI / 2) + _rng.Uniform(-0.8, 0.8);
        }
        var distance = _rng.Uniform(4, 14);
        var landing = new PitchPoint(from.X + Math.Cos(angle) * distance, from.Y + Math.Sin(angle) * distance);
        _ball.LoftedCross = false;
        if (!Pitch.IsInside(landing))
        {
            StartFlight(FlightKind.Out, from, landing, speed: 10, null, lastTouch, peakZ: 0.4);
            return;
        }

        var nearest = _home.OnPitch.Concat(_away.OnPitch).Where(p => p != lastTouch).MinBy(p => p.Pos.DistanceTo(landing))!;
        StartFlight(FlightKind.Loose, from, landing, speed: 9, nearest, lastTouch, peakZ: 0.4);
    }

    private void Carry(SimPlayer carrier, PitchPoint target, bool underPressure)
    {
        _carryTarget = target;
        _carryUntilMs = _clockMs + (long)_rng.Uniform(1000, 2200);
        _decisionAtMs = _carryUntilMs;
        Emit(MatchEventType.Carry, carrier.Team, carrier, carrier.Pos, target, underPressure: underPressure);
    }

    private void Foul(SimPlayer fouler, SimPlayer victim)
    {
        var foul = Emit(MatchEventType.Foul, fouler.Team, fouler, victim.Pos, opponent: victim);
        Scene(SceneKind.RefereeSignal, [fouler.Id], new Dictionary<string, string> { ["signal"] = "free_kick", ["team"] = victim.Team.Id }, foul.EventId);

        var extraDelay = 0L;
        var r = _rng.NextDouble();
        if (r < 0.012 || (r < 0.18 && fouler.Yellows == 1))
        {
            var outcome = r < 0.012 ? EventOutcome.Red : EventOutcome.SecondYellow;
            Emit(MatchEventType.Card, fouler.Team, fouler, fouler.Pos, outcome: outcome, relatedEventId: foul.EventId);
            Scene(SceneKind.RefereeSignal, [fouler.Id], new Dictionary<string, string> { ["signal"] = "red_card", ["player"] = fouler.Id }, foul.EventId);
            fouler.OnPitch = false;
            extraDelay = 20000;
        }
        else if (r < 0.18)
        {
            fouler.Yellows++;
            Emit(MatchEventType.Card, fouler.Team, fouler, fouler.Pos, outcome: EventOutcome.Yellow, relatedEventId: foul.EventId);
            Scene(SceneKind.RefereeSignal, [fouler.Id], new Dictionary<string, string> { ["signal"] = "yellow_card", ["player"] = fouler.Id }, foul.EventId);
            extraDelay = 8000;
        }

        // No penalties in v0: a foul in the box restarts from the edge of the area.
        var spot = victim.Pos;
        var victimAttacksRight = AttacksRight(victim.Team);
        if (Pitch.InAttackingBox(spot, victimAttacksRight))
        {
            var (_, fromLeft) = Pitch.ToAttackFrame(spot, victimAttacksRight);
            spot = Pitch.FromAttackFrame(Pitch.Length - Pitch.BoxDepth - 1.5, fromLeft, victimAttacksRight);
        }

        SetDeadBall(MatchEventType.FreeKick, victim.Team, Pitch.Clamp(spot, -1), (long)_rng.Uniform(18000, 32000) + extraDelay);
    }

    private bool IsOffside(SimPlayer passer, SimPlayer receiver, bool attackRight)
    {
        var (receiverDepth, _) = Pitch.ToAttackFrame(receiver.Pos, attackRight);
        var (ballDepth, _) = Pitch.ToAttackFrame(passer.Pos, attackRight);
        if (receiverDepth <= Pitch.Length / 2 || receiverDepth <= ballDepth || _restartInProgress is MatchEventType.ThrowIn or MatchEventType.Corner or MatchEventType.GoalKick)
        {
            return false;
        }

        return receiverDepth > OffsideLineDepth(passer.Team) + 1.0;
    }

    /// <summary>Depth (in the attacking team's frame) of the second-last defender.</summary>
    private double OffsideLineDepth(SimTeam attacking)
    {
        var attackRight = AttacksRight(attacking);
        var depths = attacking.Opponent.OnPitch
            .Select(p => Pitch.ToAttackFrame(p.Pos, attackRight).Depth)
            .OrderByDescending(d => d)
            .ToList();
        return depths.Count >= 2 ? depths[1] : Pitch.Length / 2;
    }

    private PitchPoint CarryTarget(SimPlayer carrier, bool attackRight)
    {
        var (depth, fromLeft) = Pitch.ToAttackFrame(carrier.Pos, attackRight);
        var (nearest, _) = NearestOpponent(carrier);
        var sidestep = 0.0;
        if (nearest is not null)
        {
            var (_, oppLeft) = Pitch.ToAttackFrame(nearest.Pos, attackRight);
            sidestep = oppLeft > fromLeft ? -3 : 3;
        }

        var target = Pitch.FromAttackFrame(Math.Min(depth + _rng.Uniform(5, 10), Pitch.Length - 3), Math.Clamp(fromLeft + sidestep, 2, Pitch.Width - 2), attackRight);
        return target;
    }

    private long DecisionDelayMs(SimPlayer player, bool underPressure) =>
        underPressure ? (long)_rng.Uniform(700, 1700) : (long)_rng.Uniform(1300, 3200);

    private (SimPlayer? Player, double Distance) NearestOpponent(SimPlayer player)
    {
        SimPlayer? best = null;
        var bestDistance = double.MaxValue;
        foreach (var o in player.Team.Opponent.OnPitch)
        {
            var d = o.Pos.DistanceTo(player.Pos);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = o;
            }
        }

        return (best, bestDistance);
    }

    private double NearestOpponentDistance(SimPlayer player) => NearestOpponent(player).Distance;
}
