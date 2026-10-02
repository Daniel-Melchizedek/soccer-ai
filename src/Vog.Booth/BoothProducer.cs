using Vog.Contracts;
using Vog.Stats;

namespace Vog.Booth;

public enum BoothEventKind
{
    Started,
    Finished,
    Dropped,
}

public sealed record BoothEvent(BoothEventKind Kind, Utterance Utterance);

/// <summary>
/// The airtime arbiter for one audio channel (language × mode). Audio is serial, so describers and
/// agents only *bid* with intents; the producer decides what is said, when, and what is dropped.
/// <list type="number">
/// <item>Never overlaps two utterances on a channel.</item>
/// <item>P0 (goal, red card, period end) cuts in at the next word boundary; P1 (tackle, shot, foul) cuts in over P2+.</item>
/// <item>Urgent items air in the order they happened.</item>
/// <item>In open play the ball's location is mentioned within the policy's maximum gap.</item>
/// <item>Stale items (not started by their ValidUntil) and superseded items are dropped rather than read late.</item>
/// <item>P3/P4 colour only airs during a dead ball, if it fits before the restart.</item>
/// </list>
/// </summary>
public sealed class BoothProducer
{
    /// <summary>How far ahead of the gap limit a location mention is started.</summary>
    public const long LocationLeadMs = 1500;

    /// <summary>A preempted utterance stops at the next word boundary, within this time.</summary>
    public const long PreemptWithinMs = 250;

    /// <summary>How much of a shot call plays before its outcome may cut in ("Zandor shoots…").</summary>
    public const long MinShotCallMs = 1000;

    /// <summary>A P1 item cuts in over a playing P1 only if it matters this much more (a shot over a tackle).</summary>
    public const double UrgentOutrankMargin = 0.2;

    /// <summary>How much of an out-of-date possession call plays before the newer one cuts in.</summary>
    public const long MinSupersededCallMs = 700;

    private readonly ProducerPolicy _policy;
    private readonly Func<UtteranceIntent, string> _realize;
    private readonly Func<BallState, long, UtteranceIntent?> _ballLocation;
    private readonly Func<BallState, long, UtteranceIntent> _scoreTime;
    private readonly List<Pending> _queue = [];
    private readonly List<Utterance> _timeline = [];
    private readonly List<Utterance> _dropped = [];

    private Playing? _current;
    private long _lastLocationMentionMs = long.MinValue / 2;
    private long _lastScoreTimeMs;
    private long _deadSinceMs;
    private BallStatus _status = BallStatus.Dead;
    private long _utteranceSeq;

    public BoothProducer(
        string channelId,
        ProducerPolicy policy,
        Func<UtteranceIntent, string> realize,
        Func<BallState, long, UtteranceIntent?> ballLocation,
        Func<BallState, long, UtteranceIntent> scoreTime)
    {
        ChannelId = channelId;
        _policy = policy;
        _realize = realize;
        _ballLocation = ballLocation;
        _scoreTime = scoreTime;
    }

    public string ChannelId { get; }

    public ProducerPolicy Policy => _policy;

    /// <summary>Utterances that played (fully or until preempted), in order.</summary>
    public IReadOnlyList<Utterance> Timeline => _timeline;

    public IReadOnlyList<Utterance> Dropped => _dropped;

    public bool IsSpeaking => _current is not null;

    public void Enqueue(UtteranceIntent intent, long nowMs)
    {
        if (intent.Kind == IntentKind.GraphicReadout && !_policy.ReadGraphics)
        {
            return;
        }

        if (intent.SupersedeKey is { } key)
        {
            foreach (var old in _queue.Where(q => q.Intent.SupersedeKey == key && q.Intent.Priority >= intent.Priority).ToList())
            {
                Drop(old, nowMs, UtteranceStatus.DroppedSuperseded, events: null);
            }
        }

        _queue.Add(Prepare(intent));
    }

    public IReadOnlyList<BoothEvent> Tick(long nowMs, BallState ball)
    {
        var events = new List<BoothEvent>();
        TrackStatus(nowMs, ball);

        if (_current is { } playing && nowMs >= playing.EndMs)
        {
            Finish(playing, events);
        }

        // ValidUntil is the latest time an item may *start*: after that it describes the past.
        foreach (var stale in _queue.Where(q => q.Intent.ValidUntilMs < nowMs).ToList())
        {
            Drop(stale, nowMs, UtteranceStatus.DroppedStale, events);
        }

        // P0 cuts in over anything; P1 cuts in over routine play-by-play and colour (P2+),
        // as a describer breaks off mid-phrase for a tackle or a shot.
        // A shot's outcome may also break into the shot call itself ("shoots from twen— saved!"),
        // once enough of the call has played for the listener to know who shot.
        if (_current is { Preempted: false } cur && cur.Item.Intent.Interruptible)
        {
            var onAir = cur.Item.Intent;
            var hardCut = _queue.Any(q => q.Intent.Priority == Priority.P0
                || (q.Intent.Priority == Priority.P1 && onAir.Priority >= Priority.P2)
                || (q.Intent.Priority == Priority.P1 && onAir.Priority == Priority.P1 && q.Intent.Value >= onAir.Value + UrgentOutrankMargin));
            var outcomeCut = onAir.Kind == IntentKind.Shot && _queue.Any(q => q.Intent.Kind == IntentKind.ShotOutcome);

            // The ball changed hands again: what's on air is already out of date.
            var supersededOnAir = onAir.Priority == Priority.P1 && onAir.SupersedeKey is { } key
                && _queue.Any(q => q.Intent.Priority == Priority.P1 && q.Intent.SupersedeKey == key && q.Intent.PtsMs > onAir.PtsMs);

            if (hardCut || outcomeCut || supersededOnAir)
            {
                var earliest = hardCut ? nowMs : cur.StartMs + (outcomeCut ? MinShotCallMs : MinSupersededCallMs);
                cur.EndMs = Math.Min(cur.EndMs, Math.Max(earliest, nowMs) + PreemptWithinMs);
                cur.Preempted = true;
            }
        }

        if (_current is null && ChooseNext(nowMs, ball) is { } next)
        {
            Start(next, nowMs, events);
        }

        return events;
    }

    /// <summary>Ends whatever is playing (end of feed).</summary>
    public IReadOnlyList<BoothEvent> Flush(long nowMs)
    {
        var events = new List<BoothEvent>();
        if (_current is { } playing)
        {
            playing.EndMs = Math.Max(playing.StartMs, Math.Min(playing.EndMs, nowMs));
            Finish(playing, events);
        }

        foreach (var q in _queue.ToList())
        {
            Drop(q, nowMs, UtteranceStatus.DroppedStale, events);
        }

        return events;
    }

    public long EstimateMs(string text)
    {
        var words = 0.0;
        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // Numbers take several spoken words ("one hundred and four").
            words += token.Any(char.IsDigit) ? 3 : 1;
        }

        return (long)(words / _policy.WordsPerSecond * 1000) + 250;
    }

    private Pending? ChooseNext(long nowMs, BallState ball)
    {
        var inPlay = ball.Status == BallStatus.InPlay;

        // Urgent items air in the order they happened: a save is never called before the shot.
        var urgent = _queue.Where(q => q.Intent.Priority <= Priority.P1).OrderBy(q => q.Intent.Priority).ThenBy(q => q.Intent.PtsMs).FirstOrDefault();
        if (urgent is not null)
        {
            return Take(urgent);
        }

        if (inPlay && nowMs - _lastLocationMentionMs >= _policy.BallLocationMaxGapMs - LocationLeadMs)
        {
            var located = _queue.Where(q => q.Intent.MentionsBallLocation && q.Intent.Priority == Priority.P2).OrderByDescending(q => q.Intent.PtsMs).FirstOrDefault();
            if (located is not null)
            {
                return Take(located);
            }

            // While a pass is in the air its own call is moments away; only fill if the gap is due now.
            var due = !ball.InFlight || nowMs - _lastLocationMentionMs >= _policy.BallLocationMaxGapMs;
            if (due && _ballLocation(ball, nowMs) is { } location)
            {
                return Prepare(location);
            }
        }

        var regular = _queue.Where(q => q.Intent.Priority == Priority.P2).OrderByDescending(q => q.Intent.Value).ThenBy(q => q.Intent.PtsMs).FirstOrDefault();
        if (regular is not null)
        {
            return Take(regular);
        }

        var calmMoment = !inPlay || ball.Zone?.Third != PitchThird.Attacking;
        if (calmMoment && nowMs - _lastScoreTimeMs >= _policy.ScoreTimeIntervalMs)
        {
            return Prepare(_scoreTime(ball, nowMs));
        }

        if (!inPlay)
        {
            var remaining = ExpectedRestartMs(ball) - nowMs;
            var colour = _queue.Where(q => q.Intent.Priority >= Priority.P3 && q.EstimateMs <= remaining)
                .OrderBy(q => q.Intent.Priority).ThenByDescending(q => q.Intent.Value).FirstOrDefault();
            if (colour is not null)
            {
                return Take(colour);
            }
        }

        return null;
    }

    private long ExpectedRestartMs(BallState ball) => _deadSinceMs + ball.PendingRestart switch
    {
        MatchEventType.ThrowIn => 12_000,
        MatchEventType.GoalKick => 20_000,
        MatchEventType.Corner => 25_000,
        MatchEventType.FreeKick => 18_000,
        MatchEventType.KickOff => 45_000,
        _ => 300_000,
    };

    private void TrackStatus(long nowMs, BallState ball)
    {
        if (ball.Status == BallStatus.Dead && _status == BallStatus.InPlay)
        {
            _deadSinceMs = nowMs;
        }

        _status = ball.Status;
    }

    private Pending Take(Pending item)
    {
        _queue.Remove(item);
        return item;
    }

    private Pending Prepare(UtteranceIntent intent)
    {
        var text = _realize(intent);
        return new Pending(intent, text, EstimateMs(text));
    }

    private void Start(Pending item, long nowMs, List<BoothEvent> events)
    {
        var id = $"{ChannelId}-u{++_utteranceSeq:D6}";
        _current = new Playing(id, item, nowMs, nowMs + item.EstimateMs);
        if (item.Intent.MentionsBallLocation)
        {
            _lastLocationMentionMs = nowMs;
        }

        if (item.Intent.Kind == IntentKind.ScoreTime)
        {
            _lastScoreTimeMs = nowMs;
        }

        events.Add(new BoothEvent(BoothEventKind.Started, ToUtterance(_current, UtteranceStatus.Done)));
    }

    private void Finish(Playing playing, List<BoothEvent> events)
    {
        var utterance = ToUtterance(playing, playing.Preempted ? UtteranceStatus.Preempted : UtteranceStatus.Done);
        _timeline.Add(utterance);
        _current = null;
        events.Add(new BoothEvent(BoothEventKind.Finished, utterance));
    }

    private void Drop(Pending item, long nowMs, UtteranceStatus status, List<BoothEvent>? events)
    {
        _queue.Remove(item);
        var utterance = new Utterance($"{ChannelId}-d{++_utteranceSeq:D6}", item.Intent, ChannelId, item.Text, nowMs, nowMs, status);
        _dropped.Add(utterance);
        events?.Add(new BoothEvent(BoothEventKind.Dropped, utterance));
    }

    private Utterance ToUtterance(Playing p, UtteranceStatus status) =>
        new(p.Id, p.Item.Intent, ChannelId, p.Item.Text, p.StartMs, p.EndMs, status);

    private sealed record Pending(UtteranceIntent Intent, string Text, long EstimateMs);

    private sealed class Playing(string id, Pending item, long startMs, long endMs)
    {
        public string Id { get; } = id;

        public Pending Item { get; } = item;

        public long StartMs { get; } = startMs;

        public long EndMs { get; set; } = endMs;

        public bool Preempted { get; set; }
    }
}
