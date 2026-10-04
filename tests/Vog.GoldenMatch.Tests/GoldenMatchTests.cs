using Vog.Contracts;
using Vog.Pipeline;

namespace Vog.GoldenMatch.Tests;

/// <summary>Plays the golden match once, on five channels at once, and shares the result.</summary>
public sealed class GoldenRun
{
    public static readonly ChannelSpec[] Channels =
    [
        new("en-GB", ListenerMode.AudioDescription),
        new("es-ES", ListenerMode.AudioDescription),
        new("en-GB", ListenerMode.Commentary),
        new("en-GB", ListenerMode.Calm),
        new("ta-IN", ListenerMode.AudioDescription),
    ];

    public OfflineMatchResult Result { get; } = OfflineMatchRun.Run(Pipeline.GoldenMatch.Setup(), Channels);
}

public sealed class GoldenMatchTests(GoldenRun golden) : IClassFixture<GoldenRun>
{
    public static TheoryData<int> ChannelIndexes => [0, 1, 2, 3, 4];

    [Theory]
    [MemberData(nameof(ChannelIndexes))]
    public void EveryChannelMeetsTheAccessibilityTargets(int index)
    {
        var channel = golden.Result.Channels[index];
        var failures = channel.Coverage.Checks(channel.Booth.Policy).Where(c => !c.Pass).ToList();
        Assert.True(failures.Count == 0, $"{channel.Spec.Id}: " + string.Join("; ", failures.Select(f => $"{f.Metric} = {f.Value} (target {f.Target})")));
    }

    [Fact]
    public void EveryGoalIsCalledOnTheAudioDescriptionChannel()
    {
        var ad = golden.Result.Channels[0].Booth.Timeline;
        var goals = golden.Result.HomeScore + golden.Result.AwayScore;
        Assert.Equal(goals, ad.Count(u => u.Intent.Kind == IntentKind.Goal));
    }

    [Fact]
    public void BothHalvesAreBookendedByTheirCalls()
    {
        var keys = golden.Result.Channels[0].Booth.Timeline.Select(u => u.Intent.TemplateKey).ToList();
        Assert.Equal("pbp.kickoff", keys.First(k => k.StartsWith("pbp.", StringComparison.Ordinal)));
        Assert.Contains("pbp.halftime", keys);
        Assert.Contains("pbp.second_half", keys);
        Assert.Equal("pbp.fulltime", keys.Last(k => k.StartsWith("pbp.", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheSameSeedProducesTheSameCommentary()
    {
        var first = OfflineMatchRun.Run(Pipeline.GoldenMatch.Setup(), [GoldenRun.Channels[0]], stopAfterMinute: 12);
        var second = OfflineMatchRun.Run(Pipeline.GoldenMatch.Setup(), [GoldenRun.Channels[0]], stopAfterMinute: 12);

        var a = first.Channels[0].Booth.Timeline.Select(u => $"{u.StartPtsMs}:{u.Text}").ToList();
        var b = second.Channels[0].Booth.Timeline.Select(u => $"{u.StartPtsMs}:{u.Text}").ToList();
        Assert.True(a.Count > 100);
        Assert.Equal(a, b);
    }
}
