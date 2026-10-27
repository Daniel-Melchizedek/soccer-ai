using Vog.Contracts;
using Vog.Simulator;

namespace Vog.Pipeline;

/// <summary>
/// Matches run back to back from <see cref="StartsAt"/>, each followed by a break.
/// The default start is <see cref="Epoch"/>; gateways can supply their first-listener kickoff.
/// Matching schedules and simulation options are required to synchronize separate gateways.
/// </summary>
public sealed record MatchSchedule(long MatchLengthMs, TimeSpan Break, double Speed)
{
    public static readonly DateTimeOffset Epoch = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    public DateTimeOffset StartsAt { get; init; } = Epoch;

    /// <summary>Wall-clock length of one match and its break.</summary>
    public TimeSpan Cycle => TimeSpan.FromMilliseconds(MatchLengthMs / Speed) + Break;

    /// <summary>
    /// The match time the schedule has reached at <paramref name="now"/>, or null during the break,
    /// with how long until the next kick-off.
    /// </summary>
    public (long? MatchMs, TimeSpan UntilKickOff) At(DateTimeOffset now)
    {
        if (now < StartsAt)
        {
            return (null, StartsAt - now);
        }

        var intoCycle = TimeSpan.FromTicks((now - StartsAt).Ticks % Cycle.Ticks);
        var matchMs = (long)(intoCycle.TotalMilliseconds * Speed);
        return matchMs < MatchLengthMs ? (matchMs, TimeSpan.Zero) : (null, Cycle - intoCycle);
    }

    /// <summary>How long the match runs, by simulating it (the simulator alone takes about a second).</summary>
    public static long LengthOf(MatchSetup setup) =>
        new MatchEngine(setup).Run().Select(t => t.Frame.PtsMs).LastOrDefault() + TrackingFrame.IntervalMs;
}
