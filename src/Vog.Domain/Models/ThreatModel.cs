using Vog.Contracts;

namespace Vog.Domain.Models;

/// <summary>
/// A closed-form stand-in for expected threat (xT): the value of having the ball at a point,
/// rising sharply towards the centre of the goal being attacked. tools/Vog.Train will replace it
/// with a 12×8 grid solved by value iteration over simulated seasons.
/// </summary>
public static class ThreatModel
{
    public static double Value(PitchPoint p, bool attackingRight)
    {
        var distance = Pitch.DistanceToGoal(p, attackingRight);
        var (_, fromLeft) = Pitch.ToAttackFrame(p, attackingRight);
        var central = 1 - Math.Min(1, Math.Abs(fromLeft - Pitch.Width / 2) / (Pitch.Width / 2));
        return 0.008 + 0.32 * Math.Exp(-distance / 11) * (0.55 + 0.45 * central);
    }
}
