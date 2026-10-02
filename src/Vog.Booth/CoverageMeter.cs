using Vog.Contracts;
using Vog.Stats;

namespace Vog.Booth;

/// <summary>Accessibility quality metrics for one channel (BUILD_PLAN.md §17.1).</summary>
public sealed record CoverageReport(
    ListenerMode Mode,
    double InPlayMinutes,
    int Utterances,
    double SpeechMinutes,
    double LocationGapP50Seconds,
    double LocationGapP95Seconds,
    double LocationGapMaxSeconds,
    double DeadAirPercent,
    double UrgentLatencyP50Ms,
    double UrgentLatencyP95Ms,
    double RoutineLatencyP95Ms,
    int Overlaps,
    int Preempted,
    int UrgentStaleDrops,
    IReadOnlyDictionary<UtteranceStatus, int> DropsByStatus,
    IReadOnlyDictionary<IntentKind, int> SpokenByKind)
{
    public IReadOnlyList<(string Metric, string Value, string Target, bool Pass)> Checks(ProducerPolicy policy)
    {
        var maxGap = policy.BallLocationMaxGapMs / 1000.0;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        return
        [
            ("ball-location gap p50 (open play)", $"{LocationGapP50Seconds:0.0} s", $"≤ {maxGap * 0.75:0.0} s", LocationGapP50Seconds <= maxGap * 0.75),
            ("ball-location gap p95 (open play)", $"{LocationGapP95Seconds:0.0} s", $"≤ {maxGap * 1.25:0.0} s", LocationGapP95Seconds <= maxGap * 1.25),
            ("dead air in open play (> 4 s silent)", $"{DeadAirPercent:0.0} %", "≤ 5 %", DeadAirPercent <= 5),
            ("urgent (P0/P1) event → speech p95", $"{UrgentLatencyP95Ms:0} ms", "≤ 1500 ms", UrgentLatencyP95Ms <= 1500),
            ("routine (P2) event → speech p95", $"{RoutineLatencyP95Ms:0} ms", "≤ 3000 ms", RoutineLatencyP95Ms <= 3000),
            ("overlapping utterances", Overlaps.ToString(invariant), "0", Overlaps == 0),
            ("urgent items lost (stale P0/P1)", UrgentStaleDrops.ToString(invariant), "0", UrgentStaleDrops == 0),
        ];
    }
}

/// <summary>Records ball status per tick and scores a booth's timeline against the targets.</summary>
public sealed class CoverageMeter
{
    private const long DeadAirThresholdMs = 4000;

    private readonly List<(long Pts, bool InPlay)> _ticks = [];

    public void Observe(long ptsMs, BallState ball) => _ticks.Add((ptsMs, ball.Status == BallStatus.InPlay));

    public CoverageReport Report(BoothProducer booth)
    {
        var timeline = booth.Timeline.OrderBy(u => u.StartPtsMs).ToList();
        var tickMs = TrackingFrame.IntervalMs;

        // Ball-location gaps: open-play time between mentions.
        var mentions = timeline.Where(u => u.Intent.MentionsBallLocation).Select(u => u.StartPtsMs).ToList();
        var gaps = new List<double>();
        var accumulated = 0L;
        var m = 0;
        foreach (var (pts, inPlay) in _ticks)
        {
            while (m < mentions.Count && mentions[m] <= pts)
            {
                if (accumulated > 0)
                {
                    gaps.Add(accumulated / 1000.0);
                }

                accumulated = 0;
                m++;
            }

            if (inPlay)
            {
                accumulated += tickMs;
            }
        }

        // Dead air: open-play ticks with nothing playing and the last speech more than 4 s ago.
        var deadAir = 0L;
        var inPlayMs = 0L;
        var lastEnd = long.MinValue / 2;
        var u = 0;
        foreach (var (pts, inPlay) in _ticks)
        {
            while (u < timeline.Count && timeline[u].EndPtsMs <= pts)
            {
                lastEnd = Math.Max(lastEnd, timeline[u].EndPtsMs);
                u++;
            }

            var speaking = u < timeline.Count && timeline[u].StartPtsMs <= pts;
            if (inPlay)
            {
                inPlayMs += tickMs;
                if (!speaking && pts - lastEnd > DeadAirThresholdMs)
                {
                    deadAir += tickMs;
                }
            }
        }

        var eventDriven = timeline.Where(x => x.Intent.Source == IntentSource.Template).ToList();
        var urgentLatencies = eventDriven.Where(x => x.Intent.Priority <= Priority.P1).Select(x => (double)(x.StartPtsMs - x.Intent.PtsMs)).ToList();
        // Routine = P2 play-by-play. P3/P4 colour deliberately waits for a dead ball, so it isn't latency.
        var routineLatencies = eventDriven.Where(x => x.Intent.Priority == Priority.P2).Select(x => (double)(x.StartPtsMs - x.Intent.PtsMs)).ToList();

        var overlaps = 0;
        for (var i = 1; i < timeline.Count; i++)
        {
            if (timeline[i].StartPtsMs < timeline[i - 1].EndPtsMs)
            {
                overlaps++;
            }
        }

        return new CoverageReport(
            booth.Policy.Mode,
            inPlayMs / 60_000.0,
            timeline.Count,
            timeline.Sum(x => x.DurationMs) / 60_000.0,
            Percentile(gaps, 0.5),
            Percentile(gaps, 0.95),
            gaps.Count == 0 ? 0 : gaps.Max(),
            inPlayMs == 0 ? 0 : 100.0 * deadAir / inPlayMs,
            Percentile(urgentLatencies, 0.5),
            Percentile(urgentLatencies, 0.95),
            Percentile(routineLatencies, 0.95),
            overlaps,
            timeline.Count(x => x.Status == UtteranceStatus.Preempted),
            booth.Dropped.Count(d => d.Intent.Priority <= Priority.P1 && d.Status == UtteranceStatus.DroppedStale),
            booth.Dropped.GroupBy(d => d.Status).ToDictionary(g => g.Key, g => g.Count()),
            timeline.GroupBy(x => x.Intent.Kind).ToDictionary(g => g.Key, g => g.Count()));
    }

    private static double Percentile(List<double> values, double p)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToList();
        var index = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }
}
