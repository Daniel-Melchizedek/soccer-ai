using Vog.Contracts;
using Vog.Domain;

namespace Vog.Simulator;

public sealed partial class MatchEngine
{
    private const double JogFraction = 0.42;
    private const double SprintAcceleration = 4.5;
    private const double JogAcceleration = 2.8;

    private PitchPoint KickOffPosition(SimPlayer p)
    {
        var (_, depth, fromLeft) = Formation.FourThreeThree[p.Slot];
        var attackRight = AttacksRight(p.Team);
        return Pitch.FromAttackFrame(Math.Min(depth * 0.62, 48), fromLeft, attackRight);
    }

    private void MovePlayers()
    {
        var ballPos = _ball.Mode == BallMode.Dead ? _ball.RestartSpot : _ball.Pos;
        var inPossession = _ball.Mode == BallMode.Dead ? _ball.RestartTeam : _possessionTeam;
        var t = _clockMs / 1000.0;

        foreach (var team in new[] { _home, _away })
        {
            var attackRight = AttacksRight(team);
            var attacking = team == inPossession;
            var (ballDepth, ballFromLeft) = Pitch.ToAttackFrame(ballPos, attackRight);
            var offsideLine = OffsideLineDepth(team);

            foreach (var p in team.OnPitch)
            {
                p.Sprint = false;
                p.Target = ShapeTarget(p, attacking, attackRight, ballDepth, ballFromLeft, offsideLine, t);
            }

            if (_ball.Mode == BallMode.Dead)
            {
                ApplySetPieceRoles(team, attacking, attackRight);
            }
            else if (attacking)
            {
                ApplySupportRuns(team, attackRight);
            }
            else
            {
                ApplyPressing(team, ballPos, attackRight);
            }
        }

        // Ball-specific overrides: the receiver runs onto the ball; the carrier carries.
        if (_ball.Mode == BallMode.InFlight && _ball.Target is not null)
        {
            _ball.Target.Target = _ball.To;
            _ball.Target.Sprint = _ball.Target.Pos.DistanceTo(_ball.To) > 4;
        }
        else if (_ball.Mode == BallMode.Owned && _ball.Owner is not null)
        {
            var o = _ball.Owner;
            o.Target = _clockMs < _carryUntilMs ? _carryTarget : o.Pos;
            o.Sprint = _clockMs < _carryUntilMs && NearestOpponentDistance(o) > 4;
        }
        else if (_ball.Mode == BallMode.Dead)
        {
            var taker = RestartTaker(_ball.RestartType, _ball.RestartTeam!, _ball.RestartSpot);
            taker.Target = _ball.RestartSpot;
        }

        var minute = _clockMs / 60_000.0;
        foreach (var p in _home.OnPitch.Concat(_away.OnPitch))
        {
            Step(p, minute, carrying: _ball.Owner == p);
        }
    }

    private PitchPoint ShapeTarget(SimPlayer p, bool attacking, bool attackRight, double ballDepth, double ballFromLeft, double offsideLine, double t)
    {
        var (role, baseDepth, baseLeft) = Formation.FourThreeThree[p.Slot];

        if (role == Role.Goalkeeper)
        {
            var gkDepth = attacking ? Math.Clamp(ballDepth * 0.15, 6, 16) : Math.Clamp(ballDepth * 0.05 + 2, 2, 8);
            var gkLeft = Pitch.Width / 2 + (ballFromLeft - Pitch.Width / 2) * 0.2;
            return Pitch.FromAttackFrame(gkDepth, gkLeft, attackRight);
        }

        double depth;
        double fromLeft;
        if (attacking)
        {
            depth = baseDepth + (ballDepth - 45) * 0.8 + 6;
            fromLeft = Pitch.Width / 2 + (baseLeft - Pitch.Width / 2) * 1.15 + (ballFromLeft - Pitch.Width / 2) * 0.15;
            var maxDepth = role switch
            {
                Role.CentreBack => 66,
                Role.FullBack => 88,
                Role.DefensiveMidfielder => 80,
                Role.CentralMidfielder => 92,
                _ => 101,
            };
            depth = Math.Min(depth, maxDepth);
        }
        else
        {
            depth = baseDepth + (ballDepth - 55) * 0.55 - 4;
            fromLeft = Pitch.Width / 2 + (baseLeft - Pitch.Width / 2) * 0.8 + (ballFromLeft - Pitch.Width / 2) * 0.35;
            depth = Math.Clamp(depth, role == Role.CentreBack ? 7 : 5, 72);
        }

        // Natural drift so players are never statue-still.
        var phase = p.Profile.Number * 1.7 + (p.Team.IsHome ? 0 : 0.9);
        depth += Math.Sin(t / 7.0 + phase) * 1.6;
        fromLeft += Math.Cos(t / 9.0 + phase) * 1.4;

        // Attackers hold the offside line unless the ball is already beyond it (applied last, after drift).
        if (attacking)
        {
            depth = Math.Min(depth, Math.Max(offsideLine - 2.5, ballDepth));
        }

        return Pitch.FromAttackFrame(Math.Clamp(depth, 1, Pitch.Length - 1), Math.Clamp(fromLeft, 1, Pitch.Width - 1), attackRight);
    }

    private void ApplyPressing(SimTeam team, PitchPoint ballPos, bool attackRight)
    {
        var chasers = team.OnPitch.Where(p => !p.IsGoalkeeper).OrderBy(p => p.Pos.DistanceTo(ballPos)).Take(2).ToList();
        if (chasers.Count == 0)
        {
            return;
        }

        ApplyMarking(team, attackRight, chasers);

        chasers[0].Target = ballPos;
        chasers[0].Sprint = chasers[0].Pos.DistanceTo(ballPos) > 5;

        if (chasers.Count > 1)
        {
            var ownGoal = Pitch.GoalCentre(!attackRight);
            chasers[1].Target = ballPos.MoveTowards(ownGoal, 7);
            chasers[1].Sprint = false;
        }
    }

    /// <summary>
    /// In their own half, defenders not pressing pick up the nearest attacker, goal-side.
    /// This is what stops teams walking the ball into the six-yard box.
    /// </summary>
    private void ApplyMarking(SimTeam team, bool attackRight, IReadOnlyCollection<SimPlayer> pressing)
    {
        var ownGoal = Pitch.GoalCentre(!attackRight);
        var threats = team.Opponent.OnPitch
            .Where(a => !a.IsGoalkeeper && a != _ball.Owner && Pitch.ToAttackFrame(a.Pos, attackRight).Depth < 50)
            .OrderBy(a => a.Pos.DistanceTo(ownGoal))
            .ToList();
        var markers = team.OnPitch.Where(p => !p.IsGoalkeeper && !pressing.Contains(p)).ToList();

        foreach (var attacker in threats)
        {
            var marker = markers.MinBy(m => m.Pos.DistanceTo(attacker.Pos));
            if (marker is null)
            {
                break;
            }

            markers.Remove(marker);
            var goalSide = attacker.Pos.MoveTowards(ownGoal, 1.2);
            marker.Target = PitchPoint.Lerp(marker.Target, goalSide, 0.8);
            marker.Sprint = marker.Pos.DistanceTo(goalSide) > 14;
        }
    }

    private void ApplySupportRuns(SimTeam team, bool attackRight)
    {
        if (_ball.Mode != BallMode.Owned || _ball.Owner is null || _ball.Owner.Team != team)
        {
            return;
        }

        var carrier = _ball.Owner;
        var (cDepth, cLeft) = Pitch.ToAttackFrame(carrier.Pos, attackRight);
        var supporters = team.OnPitch.Where(p => p != carrier && !p.IsGoalkeeper).OrderBy(p => p.Pos.DistanceTo(carrier.Pos)).Take(2).ToList();
        for (var i = 0; i < supporters.Count; i++)
        {
            var side = (i == 0) == (cLeft > Pitch.Width / 2) ? -1 : 1;
            var depth = Math.Min(cDepth + (i == 0 ? 6 : 14), Pitch.Length - 4);
            var left = Math.Clamp(cLeft + side * (i == 0 ? 11 : 16), 2, Pitch.Width - 2);
            supporters[i].Target = Pitch.FromAttackFrame(depth, left, attackRight);
            supporters[i].Sprint = supporters[i].Pos.DistanceTo(supporters[i].Target) > 14;
        }
    }

    private void ApplySetPieceRoles(SimTeam team, bool attacking, bool attackRight)
    {
        if (_ball.RestartType != MatchEventType.Corner)
        {
            return;
        }

        var outfield = team.OnPitch.Where(p => !p.IsGoalkeeper).ToList();
        if (attacking)
        {
            // Two stay back; the rest attack the box.
            var i = 0;
            foreach (var p in outfield.OrderBy(p => p.Slot))
            {
                if (p.Profile.Role == Role.FullBack || i >= 7)
                {
                    p.Target = Pitch.FromAttackFrame(55, p.Slot % 2 == 0 ? 20 : 48, attackRight);
                    continue;
                }

                p.Target = Pitch.FromAttackFrame(94 + (i % 3) * 2.5, 26 + (i * 3.1) % 16, attackRight);
                i++;
            }
        }
        else
        {
            var i = 0;
            foreach (var p in outfield.OrderBy(p => p.Slot))
            {
                p.Target = Pitch.FromAttackFrame(3 + (i % 3) * 2.5, 25 + (i * 2.7) % 18, attackRight);
                i++;
            }
        }
    }

    private void Step(SimPlayer p, double minute, bool carrying)
    {
        var dx = p.Target.X - p.Pos.X;
        var dy = p.Target.Y - p.Pos.Y;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        var fatigue = 1 - (1 - p.Profile.Stamina) * 0.12 * Math.Min(1, minute / 90);
        var top = p.Profile.TopSpeed * fatigue * (carrying ? 0.88 : 1);
        var max = p.Sprint ? top : top * JogFraction;
        var desired = dist < 0.05 ? 0 : Math.Min(max, dist / 0.7);

        var dvx = (dist < 0.05 ? 0 : dx / dist * desired) - p.Vx;
        var dvy = (dist < 0.05 ? 0 : dy / dist * desired) - p.Vy;
        var dv = Math.Sqrt(dvx * dvx + dvy * dvy);
        var limit = (p.Sprint ? SprintAcceleration : JogAcceleration) * Dt;
        if (dv > limit)
        {
            dvx *= limit / dv;
            dvy *= limit / dv;
        }

        p.Vx += dvx;
        p.Vy += dvy;
        var speed = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
        if (speed > top)
        {
            p.Vx *= top / speed;
            p.Vy *= top / speed;
            speed = top;
        }

        p.Pos = Pitch.Clamp(new PitchPoint(p.Pos.X + p.Vx * Dt, p.Pos.Y + p.Vy * Dt), 2);
        p.DistanceRun += speed * Dt;
    }
}
