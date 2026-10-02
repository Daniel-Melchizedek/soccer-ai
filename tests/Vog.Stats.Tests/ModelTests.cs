using Vog.Contracts;
using Vog.Domain;
using Vog.Domain.Models;

namespace Vog.Stats.Tests;

public sealed class PassModelTests
{
    private static readonly PitchPoint From = new(30, 34);

    [Fact]
    public void LongerPassesAreHarder()
    {
        var shortPass = PassModel.Difficulty(PassModel.Measure(From, new PitchPoint(40, 34), [], lofted: false, 0.75));
        var longPass = PassModel.Difficulty(PassModel.Measure(From, new PitchPoint(70, 34), [], lofted: false, 0.75));
        Assert.True(longPass > shortPass);
    }

    [Fact]
    public void ADefenderInTheLaneMakesAPassHarder()
    {
        var to = new PitchPoint(50, 34);
        var open = PassModel.Measure(From, to, [new PitchPoint(40, 50)], lofted: false, 0.75);
        var blocked = PassModel.Measure(From, to, [new PitchPoint(40, 34.5)], lofted: false, 0.75);

        Assert.Equal(0, open.LaneBlockers);
        Assert.Equal(1, blocked.LaneBlockers);
        Assert.True(PassModel.Difficulty(blocked) > PassModel.Difficulty(open));
        Assert.Contains(PassModel.Contributions(blocked), c => c.Feature == "lane_blocked" && c.LogitDelta < 0);
    }

    [Fact]
    public void ALoftedBallClearsDefendersInTheMiddleOfItsFlight()
    {
        var to = new PitchPoint(70, 34);
        var features = PassModel.Measure(From, to, [new PitchPoint(50, 34)], lofted: true, 0.75);
        Assert.Equal(0, features.LaneBlockers);
    }

    [Fact]
    public void ProbabilitiesStayBetweenZeroAndOne()
    {
        var crowded = Enumerable.Range(0, 11).Select(i => new PitchPoint(35 + i, 34)).ToList();
        var p = PassModel.SuccessProbability(PassModel.Measure(From, new PitchPoint(100, 34), crowded, lofted: false, 0.3));
        Assert.InRange(p, 0.0001, 0.9999);
    }
}

public sealed class XgModelTests
{
    [Fact]
    public void CloserAndMoreCentralShotsAreBetterChances()
    {
        var sixYards = XgModel.Probability(new PitchPoint(100, 34), attackingRight: true, header: false, underPressure: false);
        var penaltySpot = XgModel.Probability(new PitchPoint(94, 34), attackingRight: true, header: false, underPressure: false);
        var wide = XgModel.Probability(new PitchPoint(94, 14), attackingRight: true, header: false, underPressure: false);
        var longRange = XgModel.Probability(new PitchPoint(80, 34), attackingRight: true, header: false, underPressure: false);

        Assert.True(sixYards > penaltySpot);
        Assert.True(penaltySpot > wide);
        Assert.True(penaltySpot > longRange);
        Assert.InRange(penaltySpot, 0.1, 0.35);
        Assert.InRange(longRange, 0.01, 0.08);
    }

    [Fact]
    public void HeadersAndPressureLowerTheChance()
    {
        var spot = new PitchPoint(94, 34);
        var foot = XgModel.Probability(spot, true, header: false, underPressure: false);
        Assert.True(XgModel.Probability(spot, true, header: true, underPressure: false) < foot);
        Assert.True(XgModel.Probability(spot, true, header: false, underPressure: true) < foot);
    }
}

public sealed class PitchTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AttackFrameRoundTrips(bool attackingRight)
    {
        var p = new PitchPoint(23.5, 51.2);
        var (depth, fromLeft) = Pitch.ToAttackFrame(p, attackingRight);
        var back = Pitch.FromAttackFrame(depth, fromLeft, attackingRight);
        Assert.Equal(p.X, back.X, 9);
        Assert.Equal(p.Y, back.Y, 9);
    }

    [Fact]
    public void ZonesAreFromTheTeamInPossessionsPointOfView()
    {
        // Attacking right, the team's left touchline is at high Y.
        Assert.Equal(PitchChannel.Left, Pitch.ZoneFor(new PitchPoint(50, 60), attackingRight: true).Channel);
        Assert.Equal(PitchChannel.Right, Pitch.ZoneFor(new PitchPoint(50, 60), attackingRight: false).Channel);

        Assert.Equal(PitchSpot.SixYardBox, Pitch.ZoneFor(new PitchPoint(102, 34), attackingRight: true).Spot);
        Assert.Equal(PitchSpot.Box, Pitch.ZoneFor(new PitchPoint(93, 30), attackingRight: true).Spot);
        Assert.Equal(PitchSpot.OwnBox, Pitch.ZoneFor(new PitchPoint(8, 34), attackingRight: true).Spot);
        Assert.Equal(PitchSpot.CentreCircle, Pitch.ZoneFor(Pitch.Centre, attackingRight: true).Spot);
        Assert.Equal(PitchThird.Attacking, Pitch.ZoneFor(new PitchPoint(10, 34), attackingRight: false).Third);
    }
}

public sealed class MatchClockTests
{
    [Theory]
    [InlineData(1, 23 * 60_000 + 41_000, "23:41")]
    [InlineData(1, 45 * 60_000, "45:00")]
    [InlineData(1, 46 * 60_000 + 30_000, "45+2")]
    [InlineData(2, 67 * 60_000 + 5_000, "67:05")]
    [InlineData(2, 90 * 60_000 + 20_000, "90+1")]
    public void DisplaysLikeABroadcastClock(int period, long clockMs, string expected) =>
        Assert.Equal(expected, MatchClock.Display(period, clockMs));

    [Fact]
    public void MinutesForCommentary()
    {
        Assert.Equal((23, 0), MatchClock.Minute(1, 23 * 60_000 + 41_000));
        Assert.Equal((45, 2), MatchClock.Minute(1, 46 * 60_000 + 30_000));
    }
}
