using System.Diagnostics;
using System.Text.Json;
using Vog.Booth;
using Vog.Contracts;
using Vog.Pipeline;

namespace Vog.Gateway.Tests;

public sealed class BroadcastOptionsTests
{
    [Fact]
    public void AppDefaultsUseVerifiedScoringFixtureAndContinuousFastPlayback()
    {
        var defaults = new BroadcastOptions();
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json")));
        var broadcast = configuration.RootElement.GetProperty("Broadcast");
        Assert.Equal(3UL, defaults.Seed);
        Assert.Equal(60, defaults.Speed);
        Assert.Equal(0, defaults.BreakSeconds);
        Assert.Equal(defaults.Seed, broadcast.GetProperty("Seed").GetUInt64());
        Assert.Equal(defaults.Speed, broadcast.GetProperty("Speed").GetDouble());
        Assert.Equal(defaults.BreakSeconds, broadcast.GetProperty("BreakSeconds").GetDouble());
    }

    [Fact]
    public void FastPlaybackSpeaksKeyCallsWithoutQueuingEveryPass()
    {
        var options = new BroadcastOptions();
        Assert.True(options.Speak(IntentKind.Goal));
        Assert.True(options.Speak(IntentKind.PeriodChange));
        Assert.False(options.Speak(IntentKind.PbpPass));
        Assert.False(options.Speak(IntentKind.BallLocation));
        options.Speed = 1;
        Assert.All(Enum.GetValues<IntentKind>(), kind => Assert.True(options.Speak(kind)));
    }

    [Fact]
    public async Task DefaultRateDeliversThreeGoalCallsWithinTwoMinutesWithAllChannelsRunning()
    {
        var options = new BroadcastOptions();
        var channels = new[] { "en-GB", "es-ES", "ta-IN" }
            .SelectMany(lang => new[] { ListenerMode.AudioDescription, ListenerMode.Commentary, ListenerMode.Calm }
                .Select(mode => new ChannelSpec(lang, mode))).ToArray();
        var runner = new LiveMatchRunner(GoldenMatch.Setup(options.Seed), channels, options.Speed);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(115));
        var elapsed = Stopwatch.StartNew();
        var goals = new List<TimeSpan>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(matchEvent =>
        {
            if (matchEvent.Channel.Id == "en-GB:AudioDescription" && matchEvent.Event.Kind == BoothEventKind.Started && matchEvent.Event.Utterance.Intent.Kind == IntentKind.Goal)
            {
                goals.Add(elapsed.Elapsed);
                if (goals.Count == 3)
                {
                    cancellation.Cancel();
                }
            }

            return ValueTask.CompletedTask;
        }, cancellation.Token));

        Assert.Equal(3, goals.Count);
        Assert.True(goals[^1] < TimeSpan.FromMinutes(2), $"Third goal call took {goals[^1]}");
        Assert.Equal(3, runner.Pipeline.Ball!.HomeScore + runner.Pipeline.Ball.AwayScore);
    }
}