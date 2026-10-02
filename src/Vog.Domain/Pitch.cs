using Vog.Contracts;

namespace Vog.Domain;

/// <summary>
/// Pitch geometry (105 × 68 m) plus conversion to an "attack frame": depth runs from the team's
/// own goal line (0) to the goal it attacks (105), and fromLeft runs from the team's left
/// touchline (0) to its right touchline (68), as the players see it.
/// </summary>
public static class Pitch
{
    public const double Length = 105.0;
    public const double Width = 68.0;
    public const double GoalWidth = 7.32;
    public const double BoxDepth = 16.5;
    public const double BoxWidth = 40.32;
    public const double SixYardDepth = 5.5;
    public const double SixYardWidth = 18.32;
    public const double CentreCircleRadius = 9.15;

    public static PitchPoint Centre { get; } = new(Length / 2, Width / 2);

    public static PitchPoint GoalCentre(bool attackingRight) => new(attackingRight ? Length : 0, Width / 2);

    public static bool IsInside(PitchPoint p) => p.X is >= 0 and <= Length && p.Y is >= 0 and <= Width;

    public static PitchPoint Clamp(PitchPoint p, double margin = 0) =>
        new(Math.Clamp(p.X, -margin, Length + margin), Math.Clamp(p.Y, -margin, Width + margin));

    public static (double Depth, double FromLeft) ToAttackFrame(PitchPoint p, bool attackingRight) =>
        attackingRight ? (p.X, Width - p.Y) : (Length - p.X, p.Y);

    public static PitchPoint FromAttackFrame(double depth, double fromLeft, bool attackingRight) =>
        attackingRight ? new PitchPoint(depth, Width - fromLeft) : new PitchPoint(Length - depth, fromLeft);

    public static double DistanceToGoal(PitchPoint p, bool attackingRight) => p.DistanceTo(GoalCentre(attackingRight));

    /// <summary>Angle (radians) subtended by the goal mouth from <paramref name="p"/>.</summary>
    public static double GoalAngle(PitchPoint p, bool attackingRight)
    {
        var (depth, fromLeft) = ToAttackFrame(p, attackingRight);
        var toLine = Math.Max(0.1, Length - depth);
        var lateral = fromLeft - Width / 2;
        var a1 = Math.Atan2(lateral + GoalWidth / 2, toLine);
        var a2 = Math.Atan2(lateral - GoalWidth / 2, toLine);
        return Math.Abs(a1 - a2);
    }

    public static bool InAttackingBox(PitchPoint p, bool attackingRight)
    {
        var (depth, fromLeft) = ToAttackFrame(p, attackingRight);
        return depth >= Length - BoxDepth && Math.Abs(fromLeft - Width / 2) <= BoxWidth / 2;
    }

    public static Zone ZoneFor(PitchPoint p, bool attackingRight)
    {
        var (depth, fromLeft) = ToAttackFrame(p, attackingRight);
        var third = depth < Length / 3 ? PitchThird.Defensive : depth < 2 * Length / 3 ? PitchThird.Middle : PitchThird.Attacking;
        var channel = fromLeft < Width / 3 ? PitchChannel.Left : fromLeft < 2 * Width / 3 ? PitchChannel.Centre : PitchChannel.Right;
        var lateral = Math.Abs(fromLeft - Width / 2);

        var spot = PitchSpot.None;
        if (depth >= Length - SixYardDepth && lateral <= SixYardWidth / 2)
        {
            spot = PitchSpot.SixYardBox;
        }
        else if (depth >= Length - BoxDepth && lateral <= BoxWidth / 2)
        {
            spot = PitchSpot.Box;
        }
        else if (depth >= Length - BoxDepth - 6 && lateral <= BoxWidth / 2)
        {
            spot = PitchSpot.EdgeOfBox;
        }
        else if (depth >= Length - 6 && lateral >= Width / 2 - 6)
        {
            spot = PitchSpot.CornerFlag;
        }
        else if (depth <= BoxDepth && lateral <= BoxWidth / 2)
        {
            spot = PitchSpot.OwnBox;
        }
        else if (p.DistanceTo(Centre) <= CentreCircleRadius)
        {
            spot = PitchSpot.CentreCircle;
        }

        return new Zone(third, channel, spot);
    }
}
