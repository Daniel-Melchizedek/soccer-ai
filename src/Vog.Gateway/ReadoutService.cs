using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Vog.Describe;
using Vog.Pipeline;
using Vog.Stats;
using Vog.Voice;

namespace Vog.Gateway;

public sealed class ReadoutService(
    IHubContext<CommentaryHub> hub,
    ChannelDirectory directory,
    LiveBroadcastService broadcast,
    IOptions<AzureAiOptions> options,
    ILogger<ReadoutService> log) : IDisposable
{
    private readonly Uri _endpoint = options.Value.SpeechEndpoint!;
    private readonly ConcurrentDictionary<string, Lazy<Task<NeuralSynthesizer>>> _synthesizers = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();
    private readonly ConcurrentDictionary<string, byte> _prepared = new();
    private int _answerId;

    public void Prepare(ChannelSpec channel)
    {
        if (_prepared.TryAdd(channel.Id, 0))
        {
            _ = WarmAsync(channel);
        }
    }

    public async Task SendAsync(string connectionId, string kind, CancellationToken cancellationToken)
    {
        var channel = directory.ChannelOf(connectionId);
        if (channel is null)
        {
            return;
        }

        var snapshot = broadcast.Snapshot;
        if (snapshot is null)
        {
            await hub.Clients.Client(connectionId).SendAsync("answerError", "The match has not started yet.", cancellationToken);
            return;
        }

        var text = ReadoutText(channel, snapshot, kind);
        if (text is null)
        {
            await hub.Clients.Client(connectionId).SendAsync("answerError", "Choose Score or Where's the ball?", cancellationToken);
            return;
        }

        var id = -Interlocked.Increment(ref _answerId);
        var client = hub.Clients.Client(connectionId);
        await client.SendAsync("answerStart", id, cancellationToken);
        await client.SendAsync("answerText", id, text, cancellationToken);
        if (directory.IsTextOnly(connectionId))
        {
            await client.SendAsync("answerEnd", id, "completed", cancellationToken);
            return;
        }

        var gate = _gates.GetOrAdd(channel.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var synthesizer = await GetSynthesizerAsync(channel);
            await StreamAsync(synthesizer, client, id, text, cancellationToken);
            await client.SendAsync("answerEnd", id, "completed", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Quick readout failed on {Channel}", channel.Id);
            await client.SendAsync("answerEnd", id, "failed", cancellationToken);
            await client.SendAsync("answerError", "The quick readout could not be spoken. Please try again.", cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task WarmAsync(ChannelSpec channel)
    {
        var gate = _gates.GetOrAdd(channel.Id, _ => new SemaphoreSlim(1, 1));
        try
        {
            await gate.WaitAsync();
            try
            {
                var synthesizer = await GetSynthesizerAsync(channel);
                await synthesizer.SpeakAsync("Ready.");
            }
            finally
            {
                gate.Release();
            }
        }
        catch (Exception ex)
        {
            _prepared.TryRemove(channel.Id, out _);
            log.LogWarning(ex, "Quick readout voice could not be warmed on {Channel}", channel.Id);
        }
    }

    private Task<NeuralSynthesizer> GetSynthesizerAsync(ChannelSpec channel)
    {
        var lazy = _synthesizers.GetOrAdd(channel.Id, _ => new Lazy<Task<NeuralSynthesizer>>(
            () => Task.FromResult(new NeuralSynthesizer(_endpoint, AzureCredentials.Shared, VoiceCatalog.For(channel.Lang, channel.Mode)))));
        return GetValueAsync(channel.Id, lazy);
    }

    private async Task<NeuralSynthesizer> GetValueAsync(string channelId, Lazy<Task<NeuralSynthesizer>> lazy)
    {
        try
        {
            return await lazy.Value;
        }
        catch
        {
            _synthesizers.TryRemove(new KeyValuePair<string, Lazy<Task<NeuralSynthesizer>>>(channelId, lazy));
            throw;
        }
    }

    private static string? ReadoutText(ChannelSpec channel, MatchSnapshot snapshot, string kind)
    {
        var realizer = new Realizer(PhraseBankLoader.Get(channel.Lang));
        var reminders = new ReminderFactory(snapshot.Info);
        return kind.ToLowerInvariant() switch
        {
            "score" => realizer.Realize(reminders.ScoreTime(snapshot.Ball, snapshot.Frame.PtsMs)),
            "ball" when reminders.BallLocation(snapshot.Ball, snapshot.Frame.PtsMs) is { } intent => realizer.Realize(intent),
            "ball" => realizer.Bank.Phrases[snapshot.Ball.Status == BallStatus.InPlay ? "loc.ball.unclaimed" : "loc.ball.out"][0],
            _ => null,
        };
    }

    private static async Task StreamAsync(NeuralSynthesizer synthesizer, IClientProxy client, int id, string text, CancellationToken cancellationToken)
    {
        var chunks = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
        var sequence = 0;
        var sender = Task.Run(async () =>
        {
            await foreach (var data in chunks.Reader.ReadAllAsync(cancellationToken))
            {
                await client.SendAsync("answerAudio", id, sequence++, data, cancellationToken);
            }
        }, cancellationToken);

        void OnChunk(byte[] data) => chunks.Writer.TryWrite(data);
        synthesizer.AudioChunk += OnChunk;
        try
        {
            await synthesizer.SpeakAsync(text, cancellationToken);
        }
        finally
        {
            synthesizer.AudioChunk -= OnChunk;
            chunks.Writer.TryComplete();
            await sender;
        }
    }

    public void Dispose()
    {
        foreach (var lazy in _synthesizers.Values)
        {
            if (lazy.IsValueCreated && lazy.Value.IsCompletedSuccessfully)
            {
                lazy.Value.Result.Dispose();
            }
        }

        foreach (var gate in _gates.Values)
        {
            gate.Dispose();
        }
    }
}