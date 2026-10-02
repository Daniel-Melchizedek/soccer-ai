namespace Vog.Contracts;

/// <summary>
/// A position on the pitch in metres. Origin is the bottom-left corner of a 105 × 68 m pitch,
/// with X along the length and Y across the width.
/// </summary>
public readonly record struct PitchPoint(double X, double Y)
{
    public double DistanceTo(PitchPoint other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public PitchPoint MoveTowards(PitchPoint target, double distance)
    {
        var total = DistanceTo(target);
        if (total <= distance || total < 1e-9)
        {
            return target;
        }

        var f = distance / total;
        return new PitchPoint(X + (target.X - X) * f, Y + (target.Y - Y) * f);
    }

    public static PitchPoint Lerp(PitchPoint a, PitchPoint b, double t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public override string ToString() => $"({X:0.0}, {Y:0.0})";
}
