using Vog.Contracts;

namespace Vog.Domain.Models;

/// <summary>Expected-goals model: logistic on distance, goal-mouth angle, header and pressure.</summary>
public static class XgModel
{
    public static double Probability(double distanceM, double angleRad, bool header, bool underPressure)
    {
        var logit = -1.25 - 0.11 * distanceM + 1.5 * angleRad - (header ? 0.7 : 0) - (underPressure ? 0.45 : 0);
        return PassModel.Sigmoid(logit);
    }

    public static double Probability(PitchPoint location, bool attackingRight, bool header, bool underPressure) =>
        Probability(
            Pitch.DistanceToGoal(location, attackingRight),
            Pitch.GoalAngle(location, attackingRight),
            header,
            underPressure);
}
