using System.Text.Json;
using Vog.Pipeline;

namespace Vog.Companion.Tests;

/// <summary>The companion's tools, on the golden match a minute after its only goal (79:08, the Blues).</summary>
public sealed class MatchToolsTests
{
    private static readonly Lazy<MatchSnapshot> At80 = new(() =>
    {
        var pipeline = new MatchPipeline(GoldenMatch.Setup(GoldenMatch.DefaultMatchSeed), []);
        foreach (var tick in pipeline.Ticks)
        {
            pipeline.Step(tick);
            if (tick.Frame.Period == 2 && tick.Frame.MatchClockMs >= 80 * 60_000)
            {
                break;
            }
        }

        return pipeline.Snapshot!;
    });

    private static JsonElement Call(string tool, string? args = null, string lang = "en-GB") =>
        JsonDocument.Parse(new MatchTools(() => At80.Value, lang).Invoke(tool, args)).RootElement;

    [Fact]
    public void ScoreAndTimeComeFromTheMatchAndReadLikeTheCommentary()
    {
        var result = Call("get_score_and_time");

        Assert.Equal(0, result.GetProperty("home").GetProperty("goals").GetInt32());
        Assert.Equal(1, result.GetProperty("away").GetProperty("goals").GetInt32());
        Assert.Equal("second half", result.GetProperty("half").GetString());
        Assert.Equal("Reds 0, Blues 1, 80 minutes gone", result.GetProperty("spoken").GetString());
    }

    [Fact]
    public void TheScoreNamesEveryScorer()
    {
        var scorer = Assert.Single(Call("get_score_and_time").GetProperty("scorers").EnumerateArray());

        Assert.Equal("Blues", scorer.GetProperty("team").GetString());
        Assert.Contains("Zandor", scorer.GetProperty("player").GetString(), StringComparison.Ordinal);
        Assert.StartsWith("79:", scorer.GetProperty("clock").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SpanishListenersGetSpanishWords()
    {
        var spoken = Call("get_score_and_time", lang: "es-ES").GetProperty("spoken").GetString();

        Assert.Contains("Rojos", spoken, StringComparison.Ordinal);
        Assert.DoesNotContain("minutes gone", spoken, StringComparison.Ordinal);
    }

    [Fact]
    public void RecentGoalsNameTheScorer()
    {
        var events = Call("get_recent_events", """{"kinds":["goal"]}""").GetProperty("events");

        var goal = Assert.Single(events.EnumerateArray());
        Assert.Equal("goal", goal.GetProperty("kind").GetString());
        Assert.Equal("Blues", goal.GetProperty("team").GetString());
        Assert.Contains("Zandor", goal.GetProperty("player").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RecentEventsAreNewestFirstAndCapped()
    {
        var events = Call("get_recent_events", """{"count":50}""").GetProperty("events").EnumerateArray().ToList();

        Assert.Equal(MatchTools.MaxEvents, events.Count);
        var clocks = events.Select(e => e.GetProperty("clock").GetString()).ToList();
        Assert.Equal(clocks.OrderDescending(StringComparer.Ordinal), clocks);
    }

    [Fact]
    public void PlayersAreFoundByPartOfTheirName()
    {
        var player = Assert.Single(Call("get_player", """{"name":"zandor"}""").GetProperty("players").EnumerateArray());

        Assert.Equal("Blues", player.GetProperty("team").GetString());
        Assert.Contains(player.GetProperty("events").EnumerateArray(), e => e.GetProperty("kind").GetString() == "goal");
    }

    [Fact]
    public void BallAnswerSaysWhichWayEachTeamAttacks()
    {
        var result = Call("get_ball_and_possession");

        Assert.Equal(2, result.GetProperty("attacking").GetArrayLength());
        Assert.Contains("main stand", result.GetProperty("viewpoint").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StatsCoverBothTeams()
    {
        var teams = Call("get_match_stats").GetProperty("teams").EnumerateArray().ToList();

        Assert.Equal(["Reds", "Blues"], teams.Select(t => t.GetProperty("team").GetString()));
        Assert.Equal(100, teams.Sum(t => t.GetProperty("possessionPercent").GetDouble()), 1.0);
    }

    [Theory]
    [InlineData("get_recent_events", """{"count":0}""")]
    [InlineData("get_recent_events", """{"count":"lots"}""")]
    [InlineData("get_player", "{}")]
    [InlineData("get_player", """{"name":"Nobody Byname"}""")]
    [InlineData("get_player", "not json")]
    [InlineData("delete_everything", null)]
    public void BadCallsGetAnErrorTheModelCanActOn(string tool, string? args)
    {
        var error = Call(tool, args).GetProperty("error").GetString();

        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void BeforeKickOffTheToolsSaySo()
    {
        var result = JsonDocument.Parse(new MatchTools(() => null, "en-GB").Invoke("get_score_and_time", null)).RootElement;

        Assert.Contains("kicked off", result.GetProperty("status").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryToolHasAnObjectSchema()
    {
        foreach (var spec in MatchTools.Specs)
        {
            Assert.Equal("object", JsonDocument.Parse(spec.ParametersSchema).RootElement.GetProperty("type").GetString());
        }
    }
}
