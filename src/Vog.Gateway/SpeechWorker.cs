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
    private readonly Uri _endpoint;
    private readonly IHubContext<CommentaryHub> _hub;
    private readonly LatencyStats _stats;
    private readonly ILogger _log;
    private readonly Channel<(Utterance Utterance, long StartedAt)> _queue = Channel.CreateUnbounded<(Utterance, long)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private NeuralSynthesizer? _tts;

    public SpeechWorker(ChannelSpec channel, Uri endpoint, IHubContext<CommentaryHub> hub, LatencyStats stats, ILogger log)
    {
        _channel = channel;
        _endpoint = endpoint;
        _hub = hub;
        _stats = stats;
        _log = log;
        _loop = Task.Run(() => RunAsync(_stop.Token));
    }

    /// <summary>Never blocks: the booth's loop serves every channel.</summary>
    public void Enqueue(Utterance utterance) => _queue.Writer.TryWrite((utterance, Stopwatch.GetTimestamp()));

    private async Task RunAsync(CancellationToken ct)
    {
        var tts = await WarmUpAsync(ct);
        var group = _hub.Clients.Group(_channel.Id);
        await foreach (var (u, startedAt) in _queue.Reader.ReadAllAsync(ct))
        {
            // After a hold-up (warm-up, a slow request) speak what's current, not a backlog.
            if (Staleness.SkipAfter(u.Intent.Priority) is { } limit && Stopwatch.GetElapsedTime(startedAt) > limit)
            {
                _stats.RecordServerSkip();
                await group.SendAsync("skipped", u.UtteranceId, ct);
                continue;
            }

            try
            {
                await SpeakAsync(tts, group, u, startedAt, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The line is lost; ending it lets players move on to the next one.
                _log.LogError(ex, "Speech failed on {Channel} for '{Text}'", _channel.Id, u.Text);
                await group.SendAsync("audioEnd", u.UtteranceId, 0L, ct);
            }
        }
    }

    /// <summary>
    /// Connects and signs in before the first line airs (both take seconds), with a throwaway phrase
    /// that listeners never hear. Retries until it succeeds; lines queued meanwhile go stale and are
    /// skipped, so the channel starts on what's current.
    /// </summary>
    private async Task<NeuralSynthesizer> WarmUpAsync(CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        while (true)
        {
            try
            {
                _tts = new NeuralSynthesizer(_endpoint, AzureCredentials.Shared, VoiceCatalog.For(_channel.Lang, _channel.Mode));
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

    private async Task SpeakAsync(NeuralSynthesizer tts, IClientProxy group, Utterance u, long startedAt, CancellationToken ct)
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
        _queue.Writer.TryComplete();
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
