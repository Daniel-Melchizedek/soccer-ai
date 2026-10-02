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

    public async Task RunAsync(Func<ChannelBoothEvent, ValueTask> onBoothEvent, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(TrackingFrame.IntervalMs / Math.Max(0.1, speed)));
        foreach (var tick in Pipeline.Ticks)
        {
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
}
