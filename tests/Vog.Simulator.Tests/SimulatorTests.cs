using System.Security.Cryptography;
using System.Text;
using Vog.Contracts;
using Vog.Domain;
using Vog.Pipeline;
using Vog.Stats;

namespace Vog.Simulator.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalMatch() =>
        Assert.Equal(Fingerprint(7), Fingerprint(7));

    [Fact]
    public void DifferentSeeds_ProduceDifferentMatches() =>
        Assert.NotEqual(Fingerprint(7), Fingerprint(8));

    private static string Fingerprint(ulong seed, int minutes = 20)
    {
        var sb = new StringBuilder();
        foreach (var tick in new MatchEngine(GoldenMatch.Setup(seed)).Run())
        {
            sb.Append(tick.Frame.Ball.X).Append(',').Append(tick.Frame.Ball.Y).Append(';');
            foreach (var e in tick.Events)
            {
                sb.Append(e.Type).Append(e.PlayerId).Append(e.Outcome).Append('|');
            }

            if (tick.Frame.MatchClockMs > minutes * 60_000L)
            {
                break;
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }
}

public sealed class PhysicsTests
{
    [Fact]
    public void PlayersStayOnThePitchAndRunAtHumanSpeeds()
    {
        foreach (var tick in new MatchEngine(GoldenMatch.Setup(3)).Run())
        {
            foreach (var p in tick.Frame.Players)
            {
                Assert.InRange(p.X, -2.01, Pitch.Length + 2.01);
                Assert.InRange(p.Y, -2.01, Pitch.Width + 2.01);
                Assert.True(p.Speed <= 10.5, $"{p.PlayerId} ran at {p.Speed:0.0} m/s at {tick.Frame.MatchClockMs} ms");
            }
        }
    }

    [Fact]
    public void FullMatchHasTwoHalvesAndAContinuousPresentationClock()
    {
        var periods = new HashSet<int>();
        long lastPts = -1;
        foreach (var tick in new MatchEngine(GoldenMatch.Setup(4)).Run())
        {
            periods.Add(tick.Frame.Period);
            Assert.True(tick.Frame.PtsMs > lastPts);
            lastPts = tick.Frame.PtsMs;
        }

        Assert.Equal([1, 2], periods.Order());
        Assert.InRange(lastPts / 60_000.0, 92, 102);
    }
}

public sealed class RealismTests
{
    [Fact]
    public void SeededMatchesLookLikeFootball()
    {
        var completion = new List<double>();
        var goals = new List<int>();
        var shots = new List<int>();

        for (ulong seed = 1; seed <= 6; seed++)
        {
            var engine = new MatchEngine(GoldenMatch.Setup(seed));
            var stats = new StatsEngine(engine.Info);
            foreach (var tick in engine.Run())
            {
                stats.Process(tick);
            }

            stats.Flush();
            var t = stats.Totals;
            completion.Add((double)(t.Home.PassesCompleted + t.Away.PassesCompleted) / (t.Home.PassesAttempted + t.Away.PassesAttempted));
            goals.Add(t.Home.Goals + t.Away.Goals);
            shots.Add(t.Home.Shots + t.Away.Shots);

            Assert.True(t.Home.ThrowIns + t.Away.ThrowIns > 10, $"seed {seed}: too few throw-ins");
            Assert.True(t.Home.GoalKicks + t.Away.GoalKicks > 0, $"seed {seed}: no goal kicks");
            Assert.True(t.Home.Goals <= 8 && t.Away.Goals <= 8, $"seed {seed}: cricket score");
        }

        // Sanity bounds; the strict ranges live in `vog-replay datacard`.
        Assert.InRange(completion.Average(), 0.72, 0.9);
        Assert.InRange(goals.Average(), 0.8, 4.5);
        Assert.InRange(shots.Average(), 15, 35);
    }
}

public sealed class LeagueTests
{
    [Fact]
    public void LeagueIsFictionalAndComplete()
    {
        var clubs = LeagueFactory.CreateLeague(GoldenMatch.LeagueSeed);

        Assert.Equal(20, clubs.Count);
        Assert.All(clubs, c => Assert.False(LeagueFactory.IsDenied(c.Name), $"{c.Name} looks like a real club"));
        Assert.Equal(clubs.Count, clubs.Select(c => c.Name).Distinct().Count());
        Assert.All(clubs, c => Assert.Equal(18, c.Squad.Count));
        Assert.Equal("red", clubs[0].KitColour);
        Assert.Equal("blue", clubs[1].KitColour);
    }

    [Theory]
    [InlineData("Liverpool Rovers")]
    [InlineData("Manchester Town")]
    [InlineData("Ashford United")]
    public void DenyListCatchesRealClubNames(string name) =>
        Assert.True(LeagueFactory.IsDenied(name));

    [Fact]
    public void RngIsReproducibleAndInRange()
    {
        var a = new Rng(42);
        var b = new Rng(42);
        for (var i = 0; i < 1000; i++)
        {
            var x = a.NextDouble();
            Assert.Equal(x, b.NextDouble());
            Assert.InRange(x, 0, 1 - double.Epsilon);
        }
    }
}
