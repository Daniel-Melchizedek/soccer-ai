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
        PeriodicTimer? timer = null;
        try
        {
            foreach (var tick in Pipeline.Ticks)
            {
                if (timer is null && catchUpTo is not null && tick.Frame.PtsMs < catchUpTo())
                {
                    Pipeline.Step(tick);
                    cancellationToken.ThrowIfCancellationRequested();
                    continue;
                }

                timer ??= new PeriodicTimer(TimeSpan.FromMilliseconds(TrackingFrame.IntervalMs / Math.Max(0.1, speed)));
                await timer.WaitForNextTickAsync(cancellationToken);
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
        finally
        {
            timer?.Dispose();
        }
    }
}
