using Vog.Simulator;

namespace Vog.Pipeline;

/// <summary>Runs a whole match through the HOT path as fast as possible (virtual time). Deterministic.</summary>
public static class OfflineMatchRun
{
    public static OfflineMatchResult Run(
        MatchSetup setup,
        IReadOnlyList<ChannelSpec> channels,
        Action<ChannelBoothEvent>? onBoothEvent = null,
        int? stopAfterMinute = null)
    {
        var pipeline = new MatchPipeline(setup, channels);
        foreach (var tick in pipeline.Ticks)
        {
            foreach (var e in pipeline.Step(tick))
            {
                onBoothEvent?.Invoke(e);
            }

            if (stopAfterMinute is { } stop && tick.Frame.MatchClockMs >= stop * 60_000L)
            {
                break;
            }
        }

        foreach (var e in pipeline.Drain())
        {
            onBoothEvent?.Invoke(e);
        }

        return pipeline.Result();
    }
}
