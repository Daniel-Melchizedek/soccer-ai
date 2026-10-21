using Vog.Contracts;
using Vog.Pipeline;
using Vog.Simulator;

namespace Vog.GoldenMatch.Tests;

public sealed class DemoMatchTests
{
    [Fact]
    public void DefaultFixtureHasAtLeastThreeGoalsInEveryTwoMinuteViewingWindow()
    {
        var ticks = new MatchEngine(Pipeline.GoldenMatch.Setup(3)).Run().ToArray();
        var goals = ticks.Where(tick => tick.Events.Any(matchEvent => matchEvent.Type == MatchEventType.Goal))
            .Select(tick => tick.Frame.PtsMs / 60.0).ToArray();
        var cycleMs = (ticks[^1].Frame.PtsMs + TrackingFrame.IntervalMs) / 60.0;
        Assert.Equal(4, goals.Length);
        var repeated = goals.Concat(goals.Select(goal => goal + cycleMs)).Concat(goals.Select(goal => goal + 2 * cycleMs)).ToArray();
        foreach (var start in goals.Concat(goals.Select(goal => goal + 1)).Append(0))
        {
            Assert.True(repeated.Count(goal => goal >= start && goal < start + 120_000) >= 3, $"Insufficient goals from {start} ms");
        }
    }

    [Fact]
    public async Task AcceleratedRunnerKeepsUpWithWallClock()
    {
        var runner = new LiveMatchRunner(Pipeline.GoldenMatch.Setup(3), [], speed: 60);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(_ => ValueTask.CompletedTask, cancellation.Token));
        Assert.InRange(runner.Pipeline.Frame!.PtsMs, 60_000, 180_000);
    }
}