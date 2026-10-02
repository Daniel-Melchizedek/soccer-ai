using Vog.Contracts;

namespace Vog.Domain.Models;

/// <summary>
/// Logistic pass-success model. Difficulty = 1 − P(success). Each feature's contribution to the logit
/// is exposed so an explanation can say *why* a pass was hard ("lane blocked", "receiver pressed").
/// Coefficients are hand-set for v0; tools/Vog.Train will fit them on simulated seasons.
/// </summary>
public static class PassModel
{
    public const double Intercept = 3.0;
    public const double DistancePerMetre = -0.045;
    public const double PerLaneBlocker = -1.1;
    public const double ReceiverPressed = -1.0;
    public const double PasserPressed = -0.5;
    public const double LoftedPenalty = -0.35;
    public const double SkillWeight = 2.0;

    public sealed record Features(
        double DistanceM,
        int LaneBlockers,
        double ReceiverSpaceM,
        double PasserSpaceM,
        bool Lofted,
        double PasserSkill);

    public sealed record Contribution(string Feature, double LogitDelta);

    public static IReadOnlyList<Contribution> Contributions(Features f) =>
    [
        new("distance", DistancePerMetre * f.DistanceM),
        new("lane_blocked", PerLaneBlocker * Math.Min(f.LaneBlockers, 3)),
        new("receiver_pressed", ReceiverPressed * Math.Max(0, 1 - f.ReceiverSpaceM / 5)),
        new("passer_pressed", PasserPressed * Math.Max(0, 1 - f.PasserSpaceM / 3)),
        new("lofted", f.Lofted ? LoftedPenalty : 0),
        new("passer_skill", SkillWeight * (f.PasserSkill - 0.75)),
    ];

    public static double SuccessProbability(Features f) =>
        Sigmoid(Intercept + Contributions(f).Sum(c => c.LogitDelta));

    public static double Difficulty(Features f) => 1 - SuccessProbability(f);

    /// <summary>Measures the features from positions at the moment of the pass.</summary>
    public static Features Measure(
        PitchPoint from,
        PitchPoint to,
        IReadOnlyCollection<PitchPoint> opponents,
        bool lofted,
        double passerSkill)
    {
        var distance = from.DistanceTo(to);
        var blockers = 0;
        var receiverSpace = 30.0;
        var passerSpace = 30.0;

        foreach (var o in opponents)
        {
            receiverSpace = Math.Min(receiverSpace, o.DistanceTo(to));
            passerSpace = Math.Min(passerSpace, o.DistanceTo(from));

            var (t, perpendicular) = ProjectOntoSegment(from, to, o);
            if (t is < 0.08 or > 0.92)
            {
                continue;
            }

            // A lofted ball can only be cut out near where it lands.
            if (lofted && t < 0.8)
            {
                continue;
            }

            // The further the ball travels before reaching the defender, the more ground they can cover.
            var reach = 1.2 + 0.05 * t * distance;
            if (perpendicular < reach)
            {
                blockers++;
            }
        }

        return new Features(distance, blockers, receiverSpace, passerSpace, lofted, passerSkill);
    }

    public static (double T, double Perpendicular) ProjectOntoSegment(PitchPoint a, PitchPoint b, PitchPoint p)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSq = dx * dx + dy * dy;
        if (lengthSq < 1e-9)
        {
            return (0, p.DistanceTo(a));
        }

        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSq;
        var tc = Math.Clamp(t, 0, 1);
        var closest = new PitchPoint(a.X + dx * tc, a.Y + dy * tc);
        return (t, p.DistanceTo(closest));
    }

    public static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));
}
