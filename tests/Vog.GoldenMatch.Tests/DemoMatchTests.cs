using System.Diagnostics;
using Vog.Contracts;
using Vog.Pipeline;
using Vog.Simulator;

namespace Vog.GoldenMatch.Tests;

public sealed class DemoMatchTests
{
    [Fact]
    public void ExplicitSeedThreeAtSixtyTimesWithNoBreakHasThreeGoalsPerTwoMinuteWindow()
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
    public async Task NaturalRunnerTracksElapsedWallClock()
    {
        var runner = new LiveMatchRunner(Pipeline.GoldenMatch.Setup(), []);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var elapsed = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(_ => ValueTask.CompletedTask, cancellation.Token));
        Assert.InRange(runner.Pipeline.Frame!.PtsMs, 1_000, 4_000);
        Assert.InRange(runner.Pipeline.Frame.PtsMs - elapsed.Elapsed.TotalMilliseconds, -1_000, 500);
    }

    [Fact]
    public async Task NaturalCatchUpReconstructsStateWithoutEarlierCallbacks()
    {
        var runner = new LiveMatchRunner(Pipeline.GoldenMatch.Setup(), GoldenRun.Channels);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var scheduleReads = 0;
        long Target() => ++scheduleReads < 3 ? 30_000 : 30_200;
        var callbackTimes = new List<long>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(_ =>
        {
            callbackTimes.Add(runner.Pipeline.Frame!.PtsMs);
            return ValueTask.CompletedTask;
        }, cancellation.Token, Target));
        Assert.True(scheduleReads >= 3);
        Assert.InRange(runner.Pipeline.Frame!.PtsMs, 31_000, 34_200);
        Assert.All(callbackTimes, timestamp => Assert.True(timestamp >= 30_200));
        Assert.NotNull(runner.Pipeline.Info);
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