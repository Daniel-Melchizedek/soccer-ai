using System.Text.Json;
using Vog.Pipeline;

namespace Vog.Gateway.Tests;

/// <summary>The pitch stream every listener receives five times a second.</summary>
public sealed class PitchMessageTests
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

    [Fact]
    public void CarriesTheBallAndBothTeamsRoundedToTenCentimetres()
    {
        var snapshot = At80.Value;
        var message = PitchMessage.From(snapshot);

        Assert.Equal(snapshot.Frame.PtsMs, message.T);
        Assert.Equal(2, message.P);
        Assert.Equal(Math.Round(snapshot.Frame.Ball.X, 1), message.B[0]);
        Assert.Equal(3, message.B.Length);
        Assert.Equal(snapshot.Frame.Players.Count, message.H.Length + message.A.Length);
        Assert.InRange(message.H.Length, 7, 11);
        Assert.All(message.H.Concat(message.A), p => Assert.Equal(Math.Round(p[0], 1), p[0]));
    }

    [Fact]
    public void SaysWhoHasTheBallAndWhichWayTheHomeTeamAttacks()
    {
        var snapshot = At80.Value;
        var message = PitchMessage.From(snapshot);
        var expected = snapshot.Ball.PossessionTeamId is null ? null : snapshot.Ball.PossessionTeamId == snapshot.Info.Home.TeamId ? "h" : "a";

        Assert.Equal(expected, message.O);
        Assert.Equal(snapshot.Info.AttacksRight(snapshot.Info.Home.TeamId, 2), message.Hr);
    }

    [Fact]
    public void StaysSmallEnoughToSendFiveTimesASecondToEveryone()
    {
        var json = JsonSerializer.Serialize(PitchMessage.From(At80.Value), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(json.Length < 1024, $"{json.Length} bytes");
    }
}
