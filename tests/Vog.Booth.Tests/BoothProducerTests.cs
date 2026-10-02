using Vog.Contracts;
using Vog.Domain;
using Vog.Stats;

namespace Vog.Booth.Tests;

public sealed class BoothProducerTests
{
    private static readonly ProducerPolicy Ad = ProducerPolicy.For(ListenerMode.AudioDescription);

    [Fact]
    public void Property_NoOverlapNoLateStartNoLostP0AndColourOnlyAtDeadBalls()
    {
        for (ulong seed = 1; seed <= 200; seed++)
        {
            var rng = new Rng(seed);
            var booth = NewBooth(Ad);
            var ballInPlay = true;
            var seq = 0;

            for (long now = 0; now <= 120_000; now += TrackingFrame.IntervalMs)
            {
                if (rng.Chance(0.004))
                {
                    ballInPlay = !ballInPlay;
                }

                if (rng.Chance(0.12))
                {
                    var priority = rng.NextDouble() switch
                    {
                        < 0.01 => Priority.P0,
                        < 0.25 => Priority.P1,
                        < 0.8 => Priority.P2,
                        < 0.92 => Priority.P3,
                        _ => Priority.P4,
                    };
                    var valid = priority == Priority.P0 ? 20_000 : (long)rng.Uniform(1500, 8000);
                    booth.Enqueue(Intent($"i{++seq}", priority, now, valid, rng.NextDouble(), rng.Chance(0.5) ? "pbp" : null, words: rng.NextInt(2, 12)), now);
                }

                booth.Tick(now, Ball(ballInPlay, now));
            }

            booth.Flush(120_000);
            var timeline = booth.Timeline.OrderBy(u => u.StartPtsMs).ToList();
            for (var i = 1; i < timeline.Count; i++)
            {
                Assert.True(timeline[i].StartPtsMs >= timeline[i - 1].EndPtsMs, $"seed {seed}: overlap at {timeline[i].StartPtsMs}");
            }

            Assert.All(timeline, u => Assert.InRange(u.StartPtsMs, u.Intent.PtsMs, u.Intent.ValidUntilMs));
            Assert.DoesNotContain(booth.Dropped, d => d.Intent.Priority == Priority.P0 && d.Status == UtteranceStatus.DroppedStale && d.StartPtsMs < 120_000);
        }
    }

    [Fact]
    public void ColourIsOnlyReadWhileTheBallIsDead()
    {
        var booth = NewBooth(Ad);
        booth.Enqueue(Intent("colour", Priority.P3, 0, 60_000, 0.5, null, words: 4), 0);

        for (long now = 0; now <= 10_000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: true, now));
        }

        Assert.DoesNotContain(booth.Timeline, u => u.Intent.IntentId == "colour");

        for (long now = 10_100; now <= 15_000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: false, now));
        }

        Assert.Contains(booth.Timeline, u => u.Intent.IntentId == "colour");
    }

    [Fact]
    public void InOpenPlayTheBallsLocationIsMentionedWithinTheGap()
    {
        var booth = NewBooth(Ad);
        for (long now = 0; now <= 60_000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: true, now));
        }

        var mentions = booth.Timeline.Where(u => u.Intent.MentionsBallLocation).Select(u => u.StartPtsMs).ToList();
        Assert.True(mentions.Count >= 12);
        for (var i = 1; i < mentions.Count; i++)
        {
            Assert.True(mentions[i] - mentions[i - 1] <= Ad.BallLocationMaxGapMs, $"gap {mentions[i] - mentions[i - 1]} ms");
        }
    }

    [Fact]
    public void ANewerPlayByPlayItemSupersedesAQueuedOne()
    {
        var booth = NewBooth(Ad);
        booth.Enqueue(Intent("busy", Priority.P1, 0, 5000, 0.5, null, words: 10), 0);
        booth.Tick(0, Ball(inPlay: true, 0));
        booth.Enqueue(Intent("old", Priority.P2, 100, 5000, 0.5, "pbp", words: 4), 100);
        booth.Enqueue(Intent("new", Priority.P2, 200, 5000, 0.5, "pbp", words: 4), 200);

        Assert.Contains(booth.Dropped, d => d.Intent.IntentId == "old" && d.Status == UtteranceStatus.DroppedSuperseded);
    }

    [Fact]
    public void AGoalCutsInWithinAQuarterSecond()
    {
        var booth = NewBooth(Ad);

        // The pass mentions the ball's location, so it (not a reminder) opens the commentary.
        booth.Enqueue(Intent("pass", Priority.P2, 0, 5000, 0.5, null, words: 12) with { MentionsBallLocation = true }, 0);
        booth.Tick(0, Ball(inPlay: true, 0));
        booth.Enqueue(Intent("goal", Priority.P0, 500, 20_000, 1.0, null, words: 5), 500);
        for (long now = 500; now <= 3000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: true, now));
        }

        var pass = Assert.Single(booth.Timeline, u => u.Intent.IntentId == "pass");
        var goal = Assert.Single(booth.Timeline, u => u.Intent.IntentId == "goal");
        Assert.Equal(UtteranceStatus.Preempted, pass.Status);
        Assert.True(goal.StartPtsMs - 500 <= BoothProducer.PreemptWithinMs + 100);
    }

    [Fact]
    public void UrgentItemsAirInTheOrderTheyHappened()
    {
        var booth = NewBooth(Ad);
        booth.Enqueue(Intent("save", Priority.P1, 1000, 7000, 0.95, null, words: 3), 1000);
        booth.Enqueue(Intent("shot", Priority.P1, 900, 4000, 0.9, null, words: 4), 1000);
        for (long now = 1000; now <= 6000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: true, now));
        }

        var order = booth.Timeline.Where(u => u.Intent.Priority == Priority.P1).Select(u => u.Intent.IntentId).ToList();
        Assert.Equal(["shot", "save"], order);
    }

    [Fact]
    public void ModesThatDontReadGraphicsIgnoreThem()
    {
        var booth = NewBooth(ProducerPolicy.For(ListenerMode.Commentary));
        booth.Enqueue(Intent("g", Priority.P3, 0, 60_000, 0.5, null, words: 4) with { Kind = IntentKind.GraphicReadout }, 0);
        for (long now = 0; now <= 5000; now += 100)
        {
            booth.Tick(now, Ball(inPlay: false, now));
        }

        Assert.DoesNotContain(booth.Timeline, u => u.Intent.IntentId == "g");
    }

    private static BoothProducer NewBooth(ProducerPolicy policy)
    {
        var reminders = 0;
        return new BoothProducer(
            "test",
            policy,
            i => string.Join(' ', Enumerable.Repeat("word", i.Slots.TryGetValue("words", out var w) && w is NumberSlot n ? (int)n.Value : 4)),
            (ball, now) => Intent($"loc{++reminders}", Priority.P2, now, 2000, 0.5, "loc", words: 5) with { MentionsBallLocation = true, Source = IntentSource.Producer, Kind = IntentKind.BallLocation },
            (ball, now) => Intent($"rem{++reminders}", Priority.P2, now, 10_000, 0.5, "rem", words: 6) with { Source = IntentSource.Producer, Kind = IntentKind.ScoreTime });
    }

    private static UtteranceIntent Intent(string id, Priority priority, long pts, long validMs, double value, string? supersede, int words) =>
        new(id, "m", IntentKind.PbpPass, "test", new Dictionary<string, Slot> { ["words"] = new NumberSlot(words, "count") },
            priority, value, pts, pts + validMs, SupersedeKey: supersede, Interruptible: priority != Priority.P0);

    private static BallState Ball(bool inPlay, long now) =>
        new(1, now, inPlay ? BallStatus.InPlay : BallStatus.Dead, InFlight: false, "home", "p1", new PitchPoint(50, 34),
            new Zone(PitchThird.Middle, PitchChannel.Centre), inPlay ? null : MatchEventType.ThrowIn, 0, 0, 0);
}
