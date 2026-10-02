using System.Globalization;
using Vog.Contracts;
using Vog.Stats;

namespace Vog.Booth;

/// <summary>Accessibility quality metrics for one channel (BUILD_PLAN.md §17.1).</summary>
/// <param name="OpenPlayLatencyP95Ms">Pass and carry calls; null when the mode doesn't speak them (Calm).</param>
/// <param name="DeadBallLatencyP95Ms">Restart, substitution and other dead-ball calls.</param>
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
    double? OpenPlayLatencyP95Ms,
    double DeadBallLatencyP95Ms,
    int Overlaps,
    int Preempted,
    int UrgentStaleDrops,
    IReadOnlyDictionary<UtteranceStatus, int> DropsByStatus,
    IReadOnlyDictionary<IntentKind, int> SpokenByKind)
{
    /// <summary>Dead-ball calls are valid for 8 s or more: the restart is 12–30 s away.</summary>
    public const double DeadBallTargetMs = 8000;

    public IReadOnlyList<(string Metric, string Value, string Target, bool Pass)> Checks(ProducerPolicy policy)
    {
        var maxGap = policy.BallLocationMaxGapMs / 1000.0;
        var invariant = CultureInfo.InvariantCulture;
        return
        [
            ("ball-location gap p50 (open play)", $"{LocationGapP50Seconds:0.0} s", $"≤ {maxGap * 0.75:0.0} s", LocationGapP50Seconds <= maxGap * 0.75),
            ("ball-location gap p95 (open play)", $"{LocationGapP95Seconds:0.0} s", $"≤ {maxGap * 1.25:0.0} s", LocationGapP95Seconds <= maxGap * 1.25),
            ("dead air in open play (> 4 s silent)", $"{DeadAirPercent:0.0} %", "≤ 5 %", DeadAirPercent <= 5),
            ("urgent (P0/P1) event → speech p95", $"{UrgentLatencyP95Ms:0} ms", "≤ 1500 ms", UrgentLatencyP95Ms <= 1500),
            ("open-play pass calls → speech p95", OpenPlayLatencyP95Ms is { } open ? $"{open:0} ms" : "n/a", "≤ 3000 ms", OpenPlayLatencyP95Ms is null or <= 3000),
            ("dead-ball calls → speech p95", $"{DeadBallLatencyP95Ms:0} ms", $"≤ {DeadBallTargetMs:0} ms", DeadBallLatencyP95Ms <= DeadBallTargetMs),
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
        var gaps = LocationGaps(timeline);
        var (deadAirMs, inPlayMs) = DeadAir(timeline);

        var eventDriven = timeline.Where(x => x.Intent.Source == IntentSource.Template).ToList();
        var urgent = Latencies(eventDriven, x => x.Intent.Priority <= Priority.P1);
        var openPlay = Latencies(eventDriven, x => x.Intent.Priority == Priority.P2 && x.Intent.Kind is IntentKind.PbpPass or IntentKind.PbpCarry);
        var deadBall = Latencies(eventDriven, x => x.Intent.Priority == Priority.P2 && x.Intent.Kind is IntentKind.SetPiece or IntentKind.Substitution);

        return new CoverageReport(
            booth.Policy.Mode,
            inPlayMs / 60_000.0,
            timeline.Count,
            timeline.Sum(x => x.DurationMs) / 60_000.0,
            Percentile(gaps, 0.5),
            Percentile(gaps, 0.95),
            gaps.Count == 0 ? 0 : gaps.Max(),
            inPlayMs == 0 ? 0 : 100.0 * deadAirMs / inPlayMs,
            Percentile(urgent, 0.5),
            Percentile(urgent, 0.95),
            openPlay.Count == 0 ? null : Percentile(openPlay, 0.95),
            Percentile(deadBall, 0.95),
            Overlaps(timeline),
            timeline.Count(x => x.Status == UtteranceStatus.Preempted),
            booth.Dropped.Count(d => d.Intent.Priority <= Priority.P1 && d.Status == UtteranceStatus.DroppedStale),
            booth.Dropped.GroupBy(d => d.Status).ToDictionary(g => g.Key, g => g.Count()),
            timeline.GroupBy(x => x.Intent.Kind).ToDictionary(g => g.Key, g => g.Count()));
    }

    /// <summary>Open-play time between consecutive mentions of the ball's location, in seconds.</summary>
    private List<double> LocationGaps(List<Utterance> timeline)
    {
        var mentions = timeline.Where(u => u.Intent.MentionsBallLocation).Select(u => u.StartPtsMs).ToList();
        var gaps = new List<double>();
        var accumulated = 0L;
        var next = 0;
        foreach (var (pts, inPlay) in _ticks)
        {
            for (; next < mentions.Count && mentions[next] <= pts; next++)
            {
                if (accumulated > 0)
                {
                    gaps.Add(accumulated / 1000.0);
                }

                accumulated = 0;
            }

            if (inPlay)
            {
                accumulated += TrackingFrame.IntervalMs;
            }
        }

        return gaps;
    }

    /// <summary>Open-play time with nothing playing and the last speech over 4 s ago.</summary>
    private (long DeadAirMs, long InPlayMs) DeadAir(List<Utterance> timeline)
    {
        var deadAir = 0L;
        var inPlayMs = 0L;
        var lastEnd = long.MinValue / 2;
        var next = 0;
        foreach (var (pts, inPlay) in _ticks)
        {
            for (; next < timeline.Count && timeline[next].EndPtsMs <= pts; next++)
            {
                lastEnd = Math.Max(lastEnd, timeline[next].EndPtsMs);
            }

            if (!inPlay)
            {
                continue;
            }

            inPlayMs += TrackingFrame.IntervalMs;
            var speaking = next < timeline.Count && timeline[next].StartPtsMs <= pts;
            if (!speaking && pts - lastEnd > DeadAirThresholdMs)
            {
                deadAir += TrackingFrame.IntervalMs;
            }
        }

        return (deadAir, inPlayMs);
    }

    private static List<double> Latencies(List<Utterance> utterances, Func<Utterance, bool> include) =>
        utterances.Where(include).Select(x => (double)(x.StartPtsMs - x.Intent.PtsMs)).ToList();

    private static int Overlaps(List<Utterance> timeline)
    {
        var overlaps = 0;
        for (var i = 1; i < timeline.Count; i++)
        {
            if (timeline[i].StartPtsMs < timeline[i - 1].EndPtsMs)
            {
                overlaps++;
            }
        }

        return overlaps;
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
