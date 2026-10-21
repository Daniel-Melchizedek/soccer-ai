using System.Diagnostics;
using Vog.Contracts;
using Vog.Simulator;

namespace Vog.Pipeline;

/// <summary>
/// Runs a match through the HOT path in real time: one 100 ms tick per 100 ms of wall clock
/// (or faster, with <paramref name="speed"/>), handing each booth event to the caller as it happens.
/// </summary>
public sealed class LiveMatchRunner(MatchSetup setup, IReadOnlyList<ChannelSpec> channels, double speed = 1.0)
{
    public MatchPipeline Pipeline { get; } = new(setup, channels);

    /// <param name="catchUpTo">
    /// Match time to join at, read again as the catch-up runs: ticks before it are stepped at full
    /// speed and their booth events dropped, so the runner lands in step with the schedule rather than
    /// behind it by however long catching up took. Null starts from kick-off.
    /// </param>
    public async Task RunAsync(Func<ChannelBoothEvent, ValueTask> onBoothEvent, CancellationToken cancellationToken, Func<long>? catchUpTo = null)
    {
        var clock = new Stopwatch();
        long firstPts = 0;
        foreach (var tick in Pipeline.Ticks)
        {
            if (!clock.IsRunning && catchUpTo is not null && tick.Frame.PtsMs < catchUpTo())
            {
                Pipeline.Step(tick);
                cancellationToken.ThrowIfCancellationRequested();
                continue;
            }

            if (!clock.IsRunning)
            {
                firstPts = tick.Frame.PtsMs;
                clock.Start();
            }

            cancellationToken.ThrowIfCancellationRequested();
            var remaining = TimeSpan.FromMilliseconds((tick.Frame.PtsMs - firstPts) / Math.Max(0.1, speed)) - clock.Elapsed;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }

            foreach (var e in Pipeline.Step(tick))
            {
                await onBoothEvent(e);
            }
        }

        foreach (var e in Pipeline.Drain())
        {
            await onBoothEvent(e);
        }
    }
}
