using Vog.Contracts;

namespace Vog.Domain;

/// <summary>A 4-3-3 shape expressed in the attack frame (depth, fromLeft) for a neutral phase of play.</summary>
public static class Formation
{
    public static readonly IReadOnlyList<(Role Role, double Depth, double FromLeft)> FourThreeThree =
    [
        (Role.Goalkeeper, 4, 34),
        (Role.FullBack, 28, 60),
        (Role.CentreBack, 24, 43),
        (Role.CentreBack, 24, 25),
        (Role.FullBack, 28, 8),
        (Role.DefensiveMidfielder, 40, 34),
        (Role.CentralMidfielder, 50, 46),
        (Role.CentralMidfielder, 50, 22),
        (Role.Winger, 68, 60),
        (Role.Striker, 74, 34),
        (Role.Winger, 68, 8),
    ];
}
