using Vog.Contracts;
using Vog.Simulator;

namespace Vog.Pipeline;

/// <summary>
/// Kick-offs on a fixed wall-clock schedule, the same for every gateway: matches run back to back from
/// <see cref="Epoch"/>, each followed by a break. The match is deterministic, so a gateway that starts
/// mid-match fast-forwards to the scheduled moment and is then in step with every other gateway: all
/// listeners, and the agents' tools reading the deployed gateway, are at the same point of the match.
/// </summary>
public sealed record MatchSchedule(long MatchLengthMs, TimeSpan Break, double Speed)
{
    public static readonly DateTimeOffset Epoch = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Wall-clock length of one match and its break.</summary>
    public TimeSpan Cycle => TimeSpan.FromMilliseconds(MatchLengthMs / Speed) + Break;

    /// <summary>
    /// The match time the schedule has reached at <paramref name="now"/>, or null during the break,
    /// with how long until the next kick-off.
    /// </summary>
    public (long? MatchMs, TimeSpan UntilKickOff) At(DateTimeOffset now)
    {
        var intoCycle = TimeSpan.FromTicks((now - Epoch).Ticks % Cycle.Ticks);
        var matchMs = (long)(intoCycle.TotalMilliseconds * Speed);
        return matchMs < MatchLengthMs ? (matchMs, TimeSpan.Zero) : (null, Cycle - intoCycle);
    }

    /// <summary>How long the match runs, by simulating it (the simulator alone takes about a second).</summary>
    public static long LengthOf(MatchSetup setup) =>
        new MatchEngine(setup).Run().Select(t => t.Frame.PtsMs).LastOrDefault() + TrackingFrame.IntervalMs;
}
