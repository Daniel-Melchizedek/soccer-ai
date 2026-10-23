using System.Text.Json;
using System.Collections.Immutable;
using Vog.Contracts;
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
    public void PlayersAreFoundByShirtNumber()
    {
        var players = Call("get_player", """{"name":"9"}""").GetProperty("players").EnumerateArray().ToList();

        Assert.NotEmpty(players);
        Assert.All(players, player => Assert.Equal(9, player.GetProperty("number").GetInt32()));
    }

    [Fact]
    public void MissingPlayerReturnsAnEmptyResult()
    {
        var players = Call("get_player", """{"name":"Nobody Byname"}""").GetProperty("players").EnumerateArray();

        Assert.Empty(players);
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

    [Fact]
    public void RecentRecapGroundsTheGoalWithoutDuplicatingTheScoredShot()
    {
        var recap = Call("get_match_recap", """{"scope":"recent"}""");
        Assert.Equal(78 * 60_000, recap.GetProperty("startClockMs").GetInt64());
        var highlights = recap.GetProperty("highlights").EnumerateArray().ToList();
        var goal = Assert.Single(highlights, item => item.GetProperty("fact").GetProperty("kind").GetString() == "goal");
        Assert.Equal("Puts the scoring team ahead.", goal.GetProperty("why").GetString());
        Assert.Contains(At80.Value.KeyEvents, item => item.EventId == goal.GetProperty("eventId").GetString());
        Assert.DoesNotContain(highlights, item => item.GetProperty("fact").GetProperty("what").GetString() == "shot" && item.GetProperty("matchClockMs").GetInt64() == goal.GetProperty("matchClockMs").GetInt64());
    }

    [Fact]
    public void HalftimeRecapDoesNotLeakSecondHalfGoals()
    {
        var recap = Call("get_match_recap", """{"scope":"halftime"}""");
        Assert.Equal(0, recap.GetProperty("score").GetProperty("away").GetInt32());
        Assert.All(recap.GetProperty("highlights").EnumerateArray(), item => Assert.Equal(1, item.GetProperty("period").GetInt32()));
    }

    [Fact]
    public void FulltimeIsUnavailableBeforeTheWhistle()
    {
        Assert.Equal("unavailable", Call("get_match_recap", """{"scope":"fulltime"}""").GetProperty("status").GetString());
        Assert.True(Call("get_match_recap", """{"scope":"invented"}""").TryGetProperty("error", out _));
    }

    [Fact]
    public void RecentRecapClampsToSecondHalfAndIncludesExactBoundary()
    {
        var template = At80.Value.KeyEvents[0];
        var snapshot = At80.Value with
        {
            Frame = At80.Value.Frame with { Period = 2, MatchClockMs = 46 * 60_000 },
            KeyEvents = ImmutableList.Create(
                template with { EventId = "first-half-stoppage", Type = MatchEventType.Shot, Period = 1, MatchClockMs = 46 * 60_000 },
                template with { EventId = "boundary", Type = MatchEventType.Shot, Period = 2, MatchClockMs = 45 * 60_000 },
                template with { EventId = "future", Type = MatchEventType.Shot, Period = 2, MatchClockMs = 47 * 60_000 }),
        };
        var recap = JsonDocument.Parse(new MatchTools(() => snapshot, "en-GB").Invoke("get_match_recap", """{"scope":"recent"}""")).RootElement;
        Assert.Equal(45 * 60_000, recap.GetProperty("startClockMs").GetInt64());
        Assert.Equal("boundary", Assert.Single(recap.GetProperty("highlights").EnumerateArray()).GetProperty("eventId").GetString());
    }

    [Fact]
    public void FulltimeUsesRawEvidenceAndIncludesTheFinalEvent()
    {
        var end = At80.Value.KeyEvents[0] with { EventId = "end", Type = MatchEventType.PeriodEnd, Period = 2, MatchClockMs = 90 * 60_000 };
        var snapshot = At80.Value with
        {
            Frame = At80.Value.Frame with { MatchClockMs = 90 * 60_000 },
            KeyEvents = At80.Value.KeyEvents.Add(end),
        };
        var recap = JsonDocument.Parse(new MatchTools(() => snapshot, "en-GB").Invoke("get_match_recap", """{"scope":"fulltime"}""")).RootElement;
        Assert.Equal("available", recap.GetProperty("status").GetString());
        Assert.Equal(1, recap.GetProperty("score").GetProperty("away").GetInt32());
        Assert.Equal("end", recap.GetProperty("highlights").EnumerateArray().Last().GetProperty("eventId").GetString());
        Assert.Equal(snapshot.KeyEvents.Count(item => item.Type == MatchEventType.Shot), recap.GetProperty("teams").EnumerateArray().Sum(team => team.GetProperty("shots").GetInt32()));
    }

    [Fact]
    public void EmptyIntervalDoesNotInventHighlightsOrStats()
    {
        var snapshot = At80.Value with { KeyEvents = ImmutableList<MatchEvent>.Empty };
        var tools = new MatchTools(() => snapshot, "en-GB");
        var recap = JsonDocument.Parse(tools.Invoke("get_match_recap", """{"scope":"recent"}""")).RootElement;
        Assert.Empty(recap.GetProperty("highlights").EnumerateArray());
        Assert.All(recap.GetProperty("teams").EnumerateArray(), team => Assert.Equal(0, team.GetProperty("shots").GetInt32()));
        Assert.Equal("unavailable", JsonDocument.Parse(tools.Invoke("get_match_recap", """{"scope":"halftime"}""")).RootElement.GetProperty("status").GetString());
    }
}
