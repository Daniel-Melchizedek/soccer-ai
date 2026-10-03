using System.Collections.Immutable;
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

/// <summary>A booth event on a channel, with the frame it happened on (for display).</summary>
public sealed record ChannelBoothEvent(ChannelSpec Channel, BoothEvent Event, TrackingFrame Frame);

public sealed record ChannelResult(ChannelSpec Spec, BoothProducer Booth, CoverageReport Coverage);

public sealed record OfflineMatchResult(
    MatchInfo Info,
    MatchStats Totals,
    int HomeScore,
    int AwayScore,
    IReadOnlyList<ChannelResult> Channels);

/// <summary>
/// The HOT path for one match, one tick at a time: simulator (ingest) → stats (interpret) →
/// describer (language-neutral intents, computed once) → one Booth Producer per channel
/// (rendered per language and mode). Offline and live runs share this; only the pacing differs.
/// </summary>
public sealed class MatchPipeline
{
    /// <summary>Up to 30 s after the final whistle for queued calls (the full-time call) to finish.</summary>
    private const int DrainTicks = 300;

    private readonly MatchEngine _engine;
    private readonly StatsEngine _stats;
    private readonly PlayByPlayDescriber _describer;
    private readonly List<(ChannelSpec Spec, BoothProducer Booth, CoverageMeter Meter)> _channels;

    public MatchPipeline(MatchSetup setup, IReadOnlyList<ChannelSpec> channels)
    {
        _engine = new MatchEngine(setup);
        _stats = new StatsEngine(_engine.Info);
        _describer = new PlayByPlayDescriber(_engine.Info);
        var reminders = new ReminderFactory(_engine.Info);

        _channels = channels.Select(spec =>
        {
            var realizer = new Realizer(PhraseBankLoader.Get(spec.Lang));
            var booth = new BoothProducer(spec.Id, ProducerPolicy.For(spec.Mode, spec.Lang), realizer.Realize, reminders.BallLocation, reminders.ScoreTime);
            return (spec, booth, new CoverageMeter());
        }).ToList();
    }

    public MatchInfo Info => _engine.Info;

    public IEnumerable<MatchTick> Ticks => _engine.Run();

    public TrackingFrame? Frame { get; private set; }

    public BallState? Ball { get; private set; }

    public MatchStats Totals => _stats.Totals;

    /// <summary>The match as of the latest tick, for readers on other threads; null before the first tick.</summary>
    public MatchSnapshot? Snapshot => Volatile.Read(ref _snapshot);

    private MatchSnapshot? _snapshot;
    private ImmutableList<MatchEvent> _keyEvents = [];

    public IReadOnlyList<ChannelBoothEvent> Step(MatchTick tick)
    {
        var update = _stats.Process(tick);
        _keyEvents = _keyEvents.AddRange(tick.Events.Where(e => MatchSnapshot.IsKey(e.Type)));
        var now = update.Frame.PtsMs;
        var intents = _describer.Describe(update);
        var events = new List<ChannelBoothEvent>();

        foreach (var (spec, booth, meter) in _channels)
        {
            foreach (var intent in intents)
            {
                booth.Enqueue(intent, now);
            }

            foreach (var e in booth.Tick(now, update.Ball))
            {
                events.Add(new ChannelBoothEvent(spec, e, update.Frame));
            }

            meter.Observe(now, update.Ball);
        }

        Frame = update.Frame;
        Ball = update.Ball;
        var totals = _stats.Totals;
        Volatile.Write(ref _snapshot, new MatchSnapshot(
            Info, update.Frame, update.Ball,
            TeamTotals.From(totals.Home, totals.PossessionShare(totals.HomeId)),
            TeamTotals.From(totals.Away, totals.PossessionShare(totals.AwayId)),
            _keyEvents));
        return events;
    }

    /// <summary>After the final whistle: lets queued calls play out, then closes every channel.</summary>
    public IReadOnlyList<ChannelBoothEvent> Drain()
    {
        var events = new List<ChannelBoothEvent>();
        if (Frame is not { } last || Ball is not { } ball)
        {
            return events;
        }

        _stats.Flush();
        var dead = ball with { Status = BallStatus.Dead };
        foreach (var (spec, booth, _) in _channels)
        {
            for (var t = 1; t <= DrainTicks; t++)
            {
                events.AddRange(booth.Tick(last.PtsMs + t * TrackingFrame.IntervalMs, dead).Select(e => new ChannelBoothEvent(spec, e, last)));
            }

            events.AddRange(booth.Flush(last.PtsMs).Select(e => new ChannelBoothEvent(spec, e, last)));
        }

        return events;
    }

    public OfflineMatchResult Result() =>
        new(Info, _stats.Totals, _engine.HomeScore, _engine.AwayScore,
            _channels.Select(c => new ChannelResult(c.Spec, c.Booth, c.Meter.Report(c.Booth))).ToList());
}
