namespace Vog.Contracts;

/// <summary>Third of the pitch, from the point of view of the team in possession.</summary>
public enum PitchThird
{
    Defensive,
    Middle,
    Attacking,
}

/// <summary>Channel, from the point of view of the team in possession facing the goal it attacks.</summary>
public enum PitchChannel
{
    Left,
    Centre,
    Right,
}

/// <summary>Named spots a describer prefers over a generic third/channel description.</summary>
public enum PitchSpot
{
    None,
    OwnBox,
    CentreCircle,
    EdgeOfBox,
    Box,
    SixYardBox,
    CornerFlag,
}

public readonly record struct Zone(PitchThird Third, PitchChannel Channel, PitchSpot Spot = PitchSpot.None);
