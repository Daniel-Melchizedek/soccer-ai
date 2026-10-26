using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Vog.Contracts;
using Vog.Pipeline;
using Vog.Voice;

namespace Vog.Gateway;

/// <summary>
/// Speaks one channel's utterances, in order, and streams the audio to the channel's listeners as
/// it is synthesized.
/// </summary>
public sealed class SpeechWorker : IAsyncDisposable
{
    private static readonly TimeSpan WarmUpRetry = TimeSpan.FromSeconds(2);

    private readonly ChannelSpec _channel;
    private readonly Func<IStreamingSynthesizer> _createSynthesizer;
    private readonly IClientProxy _group;
    private readonly LatencyStats _stats;
    private readonly ILogger _log;
    private readonly PendingSpeechQueue _queue = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private IStreamingSynthesizer? _tts;

    public SpeechWorker(ChannelSpec channel, Uri endpoint, IHubContext<CommentaryHub> hub, LatencyStats stats, ILogger log)
        : this(channel, () => new NeuralSynthesizer(endpoint, AzureCredentials.Shared, VoiceCatalog.For(channel.Lang, channel.Mode)),
            hub.Clients.Group(channel.Id), stats, log)
    {
    }

    public SpeechWorker(ChannelSpec channel, Func<IStreamingSynthesizer> createSynthesizer, IClientProxy group, LatencyStats stats, ILogger log)
    {
        _channel = channel;
        _createSynthesizer = createSynthesizer;
        _group = group;
        _stats = stats;
        _log = log;
        _loop = Task.Run(() => RunAsync(_stop.Token));
    }

    /// <summary>Never blocks: the booth's loop serves every channel.</summary>
    public Utterance? Enqueue(Utterance utterance)
    {
        var dropped = _queue.Enqueue(utterance);
        if (dropped is not null)
        {
            _stats.RecordServerSkip();
        }
        return dropped;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var tts = await WarmUpAsync(ct);
        while (await _queue.DequeueAsync(ct) is { } pending)
        {
            var (utterance, startedAt) = pending;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (Staleness.SkipAfter(utterance.Intent.Priority) is { } limit && Stopwatch.GetElapsedTime(startedAt) > limit)
                {
                    _stats.RecordServerSkip();
                    await _group.SendAsync("skipped", utterance.UtteranceId, ct);
                    break;
                }

                var emittedAudio = 0;
                void TrackAudio(byte[] data)
                {
                    if (data.Length > 0) Interlocked.Exchange(ref emittedAudio, 1);
                }

                tts.AudioChunk += TrackAudio;
                var retry = false;
                try
                {
                    await SpeakAsync(tts, _group, utterance, startedAt, ct);
                    break;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    retry = attempt == 0 && Volatile.Read(ref emittedAudio) == 0;
                    _log.LogWarning(ex, "Speech failed on {Channel} for {UtteranceId}; retry={Retry}", _channel.Id, utterance.UtteranceId, retry);
                    if (!retry)
                    {
                        await _group.SendAsync("audioEnd", utterance.UtteranceId, 0L, ct);
                    }
                }
                finally
                {
                    tts.AudioChunk -= TrackAudio;
                }

                tts.Dispose();
                _tts = null;
                tts = await WarmUpAsync(ct);
                if (!retry) break;
            }
        }
    }

    /// <summary>
    /// Connects and signs in before the first line airs (both take seconds), with a throwaway phrase
    /// that listeners never hear. Retries until it succeeds; lines queued meanwhile go stale and are
    /// skipped, so the channel starts on what's current.
    /// </summary>
    private async Task<IStreamingSynthesizer> WarmUpAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            try
            {
                _tts = _createSynthesizer();
                await _tts.SpeakAsync(_channel.Lang.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Listo." : "Ready.", ct);
                _log.LogInformation("Speech ready on {Channel} in {Ms:0} ms", _channel.Id, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                return _tts;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Speech couldn't start on {Channel}; retrying in {Seconds} s", _channel.Id, WarmUpRetry.TotalSeconds);
                _tts?.Dispose();
                _tts = null;
                await Task.Delay(WarmUpRetry, ct);
            }
        }
    }

    private async Task SpeakAsync(IStreamingSynthesizer tts, IClientProxy group, Utterance u, long startedAt, CancellationToken ct)
    {
        // Chunks are forwarded in order by a single sender as they stream in from the service.
        var chunks = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        var seq = 0;
        var first = true;
        var sender = Task.Run(async () =>
        {
            await foreach (var data in chunks.Reader.ReadAllAsync(ct))
            {
                if (first)
                {
                    first = false;
                    _stats.RecordServerFirstChunk(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                }

                await group.SendAsync("audio", u.UtteranceId, seq++, data, ct);
            }
        }, ct);

        void OnChunk(byte[] data) => chunks.Writer.TryWrite(data);
        tts.AudioChunk += OnChunk;
        SynthesisResult result;
        try
        {
            result = await tts.SpeakAsync(u.Text, ct);
        }
        finally
        {
            tts.AudioChunk -= OnChunk;
            chunks.Writer.Complete();
            await sender;
        }

        var spokenMs = Wav.MillisecondsFor(result.Pcm.Length);
        await group.SendAsync("audioEnd", u.UtteranceId, spokenMs, ct);
        var cues = Captions.FromWords(0, result.Words, u.Text, spokenMs).Select(CaptionCueMessage.From).ToArray();
        await group.SendAsync("captions", u.UtteranceId, cues, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Complete();
        await _stop.CancelAsync();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
        }

        _tts?.Dispose();
        _stop.Dispose();
    }
}
