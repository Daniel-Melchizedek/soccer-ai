namespace Vog.Contracts;

/// <summary>Ball position (metres), height (metres) and velocity (m/s) for one frame.</summary>
public sealed record BallFrame(double X, double Y, double Z, double Vx, double Vy, string? OwnerPlayerId)
{
    public PitchPoint Position => new(X, Y);
}

public sealed record PlayerFrame(string PlayerId, string TeamId, int Jersey, double X, double Y, double Vx, double Vy)
{
    public PitchPoint Position => new(X, Y);

    public double Speed => Math.Sqrt(Vx * Vx + Vy * Vy);
}

/// <summary>A 10 Hz snapshot of the ball and every player on the pitch.</summary>
public sealed record TrackingFrame(
    string MatchId,
    long FrameId,
    int Period,
    long MatchClockMs,
    BallFrame Ball,
    IReadOnlyList<PlayerFrame> Players);
