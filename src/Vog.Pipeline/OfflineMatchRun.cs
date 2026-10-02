using Vog.Booth;
using Vog.Contracts;
using Vog.Describe;
using Vog.Simulator;
using Vog.Stats;

namespace Vog.Pipeline;

/// <summary>One audio channel: a language and a listener mode.</summary>
public sealed record ChannelSpec(string Lang, ListenerMode Mode)
{
    public string Id => $"{Lang}:{Mode}";
}

public sealed record ChannelResult(ChannelSpec Spec, BoothProducer Booth, CoverageReport Coverage);

public sealed record OfflineMatchResult(
    MatchInfo Info,
    MatchStats Totals,
    int HomeScore,
    int AwayScore,
    IReadOnlyList<ChannelResult> Channels);

/// <summary>A booth event on a channel, with the frame it happened on (for display).</summary>
public sealed record ChannelBoothEvent(ChannelSpec Channel, BoothEvent Event, TrackingFrame Frame);

/// <summary>
/// Runs the full HOT path offline, on virtual time: simulator (ingest) → stats (interpret) →
/// describer (language-neutral intents, computed once) → one Booth Producer per channel
/// (rendered per language and mode). Deterministic for a given seed.
/// </summary>
public static class OfflineMatchRun
{
    /// <summary>Up to 30 s of virtual time after the final whistle for queued calls to finish.</summary>
    private const int DrainTicks = 300;

    public static OfflineMatchResult Run(
        MatchSetup setup,
        IReadOnlyList<ChannelSpec> channels,
        Action<ChannelBoothEvent>? onBoothEvent = null,
        int? stopAfterMinute = null)
    {
        var engine = new MatchEngine(setup);
        var stats = new StatsEngine(engine.Info);
        var describer = new PlayByPlayDescriber(engine.Info);
        var reminders = new ReminderFactory(engine.Info);

        var booths = channels.Select(spec =>
        {
            var realizer = new Realizer(PhraseBankLoader.Get(spec.Lang));
            var booth = new BoothProducer(spec.Id, ProducerPolicy.For(spec.Mode, spec.Lang), realizer.Realize, reminders.BallLocation, reminders.ScoreTime);
            return (Spec: spec, Booth: booth, Meter: new CoverageMeter());
        }).ToList();

        TrackingFrame? last = null;
        BallState? lastBall = null;
        foreach (var tick in engine.Run())
        {
            var update = stats.Process(tick);
            var now = update.Frame.PtsMs;
            var intents = describer.Describe(update);

            foreach (var (spec, booth, meter) in booths)
            {
                foreach (var intent in intents)
                {
                    booth.Enqueue(intent, now);
                }

                foreach (var e in booth.Tick(now, update.Ball))
                {
                    onBoothEvent?.Invoke(new ChannelBoothEvent(spec, e, update.Frame));
                }

                meter.Observe(now, update.Ball);
            }

            last = update.Frame;
            lastBall = update.Ball;
            if (stopAfterMinute is { } stop && update.Frame.MatchClockMs >= stop * 60_000L)
            {
                break;
            }
        }

        stats.Flush();
        var results = new List<ChannelResult>();
        foreach (var (spec, booth, meter) in booths)
        {
            // After the final whistle, let whatever is queued (the full-time call) play out.
            if (last is not null && lastBall is not null)
            {
                var dead = lastBall with { Status = BallStatus.Dead };
                for (var t = 1; t <= DrainTicks; t++)
                {
                    foreach (var e in booth.Tick(last.PtsMs + t * TrackingFrame.IntervalMs, dead))
                    {
                        onBoothEvent?.Invoke(new ChannelBoothEvent(spec, e, last));
                    }
                }
            }

            foreach (var e in booth.Flush(last?.PtsMs ?? 0))
            {
                if (last is not null)
                {
                    onBoothEvent?.Invoke(new ChannelBoothEvent(spec, e, last));
                }
            }

            results.Add(new ChannelResult(spec, booth, meter.Report(booth)));
        }

        return new OfflineMatchResult(engine.Info, stats.Totals, engine.HomeScore, engine.AwayScore, results);
    }
}
