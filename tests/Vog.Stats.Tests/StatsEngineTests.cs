using Vog.Contracts;

namespace Vog.Stats.Tests;

public sealed class PlayerMotionTrackerTests
{
    [Fact]
    public void ASustainedRunAboveThresholdIsOneSprint()
    {
        var tags = Run(speedMs: 8.0, runFrames: 20);
        var sprint = Assert.Single(tags, t => t.Kind == PlayerTagKind.Sprint);
        Assert.InRange(sprint.Value, 27.5, 29.5); // 8 m/s ≈ 28.8 km/h
    }

    [Fact]
    public void AShortBurstIsNotASprint() =>
        Assert.DoesNotContain(Run(speedMs: 8.0, runFrames: 6), t => t.Kind == PlayerTagKind.Sprint);

    [Fact]
    public void ATeleportIsNotRunning()
    {
        var tracker = new PlayerMotionTracker();
        var tags = new List<PlayerTag>();
        tracker.Update(Frame(0, 10), 0.1, tags);
        tracker.Update(Frame(1, 40), 0.1, tags); // 30 m in 0.1 s: a reset, not a run
        Assert.Equal(0, tracker.Players["p1"].DistanceM);
        Assert.Empty(tags);
    }

    private static List<PlayerTag> Run(double speedMs, int runFrames)
    {
        var tracker = new PlayerMotionTracker();
        var tags = new List<PlayerTag>();
        var x = 10.0;
        long frame = 0;
        tracker.Update(Frame(frame++, x), 0.1, tags);
        for (var i = 0; i < runFrames; i++)
        {
            x += speedMs * 0.1;
            tracker.Update(Frame(frame++, x), 0.1, tags);
        }

        for (var i = 0; i < 10; i++)
        {
            tracker.Update(Frame(frame++, x), 0.1, tags);
        }

        return tags;
    }

    private static TrackingFrame Frame(long id, double x) =>
        new("m", id, 1, id * 100, new BallFrame(50, 34, 0, 0, 0, null), [new PlayerFrame("p1", "home", 9, x, 34, 0, 0)]);
}

public sealed class StatsEngineTests
{
    private static readonly MatchInfo Info = new(
        "m",
        "Test",
        new TeamInfo("home", "Home Town", "Reds", "red", [new PlayerInfo("h1", "Home One", "One", 9, Role.Striker)], []),
        new TeamInfo("away", "Away City", "Blues", "blue", [new PlayerInfo("a1", "Away One", "Uno", 5, Role.CentreBack)], []),
        Seed: 1);

    [Fact]
    public void TouchlineOutIsAThrowIn() =>
        Assert.Equal(MatchEventType.ThrowIn, PendingRestartAfterBallOut("home", new PitchPoint(40, 0)));

    [Fact]
    public void AttackerPuttingItOverTheGoalLineGivesAGoalKick() =>
        // In the first half the home team attacks the goal at X = 105.
        Assert.Equal(MatchEventType.GoalKick, PendingRestartAfterBallOut("home", new PitchPoint(105, 30)));

    [Fact]
    public void DefenderPuttingItOverTheirOwnGoalLineGivesACorner() =>
        Assert.Equal(MatchEventType.Corner, PendingRestartAfterBallOut("away", new PitchPoint(105, 30)));

    [Fact]
    public void ShotsGetXgAndPassesGetDifficulty()
    {
        var engine = new StatsEngine(Info);
        var pass = Event(1, MatchEventType.Pass, "home", "h1", new PitchPoint(40, 34), new PitchPoint(60, 34), EventOutcome.Complete);
        var shot = Event(2, MatchEventType.Shot, "home", "h1", new PitchPoint(94, 34), new PitchPoint(105, 34), EventOutcome.Saved);

        var released = new List<EnrichedEvent>();
        released.AddRange(engine.Process(Tick(0, [pass, shot])).Events);
        for (long f = 1; f <= 5; f++)
        {
            released.AddRange(engine.Process(Tick(f, [])).Events);
        }

        var p = Assert.Single(released, e => e.Event.Type == MatchEventType.Pass);
        Assert.InRange(p.Enrichment.Difficulty!.Value, 0, 1);
        Assert.Equal(20, p.Enrichment.PassDistanceM!.Value, 1);

        var s = Assert.Single(released, e => e.Event.Type == MatchEventType.Shot);
        Assert.InRange(s.Enrichment.Xg!.Value, 0.1, 0.35);
        Assert.Equal(1, engine.Totals.Home.Shots);
        Assert.Equal(1, engine.Totals.Home.ShotsOnTarget);
    }

    private static MatchEventType? PendingRestartAfterBallOut(string lastTouchTeam, PitchPoint where)
    {
        var engine = new StatsEngine(Info);
        engine.Process(Tick(0, [Event(1, MatchEventType.KickOff, "home", "h1", Vog.Domain.Pitch.Centre)]));
        var update = engine.Process(Tick(1, [Event(2, MatchEventType.BallOut, lastTouchTeam, null, where)]));
        Assert.Equal(BallStatus.Dead, update.Ball.Status);
        return update.Ball.PendingRestart;
    }

    private static MatchEvent Event(long seq, MatchEventType type, string team, string? player, PitchPoint at, PitchPoint? end = null, EventOutcome outcome = EventOutcome.None) =>
        new($"m-e{seq}", "m", seq, 1, seq * 100, "00:00", type, team, player, at, end, outcome, PossessionTeamId: team);

    private static MatchTick Tick(long frameId, IReadOnlyList<MatchEvent> events) =>
        new(new TrackingFrame("m", frameId, 1, frameId * 100 + 50, new BallFrame(40 + frameId * 2, 34, 0, 20, 0, null),
            [new PlayerFrame("h1", "home", 9, 40, 34, 0, 0), new PlayerFrame("a1", "away", 5, 70, 20, 0, 0)]), events, []);
}
