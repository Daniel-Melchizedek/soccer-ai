using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Vog.Contracts;
using Vog.Voice;

namespace Vog.Gateway.Tests;

public sealed class SpeechWorkerTests
{
    [Fact]
    public async Task WarmUpBacklogIsBoundedAndShutdownCancelsWarmUp()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var synth = new FakeSynth(async (_, _, cancellation) =>
        {
            entered.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return Result();
        });
        var worker = Worker(() => synth, new RecordingClient());
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var oldest = Call("oldest");
            Assert.Null(worker.Enqueue(oldest));
            for (var index = 1; index < 64; index++) Assert.Null(worker.Enqueue(Call($"call-{index}")));
            Assert.Same(oldest, worker.Enqueue(Call("newest")));
        }
        finally
        {
            await worker.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
        Assert.True(synth.Disposed);
        var rejected = Call("after-stop");
        Assert.Same(rejected, worker.Enqueue(rejected));
    }

    [Fact]
    public async Task FailureBeforeAudioRetriesOnceWithFreshSynthesizer()
    {
        var attempts = 0;
        var created = 0;
        var client = new RecordingClient();
        await using var worker = Worker(() =>
        {
            Interlocked.Increment(ref created);
            return new FakeSynth((synth, text, _) =>
            {
                if (text == "Ready.") return Task.FromResult(Result());
                if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("transient");
                synth.Emit();
                return Task.FromResult(Result());
            });
        }, client);
        worker.Enqueue(Call("goal"));
        await client.WaitForAsync("captions", "goal");
        Assert.Equal(2, attempts);
        Assert.Equal(2, created);
        Assert.Single(client.Messages, message => message.Method == "audio");
        Assert.Single(client.Messages, message => message.Method == "audioEnd");
    }

    [Fact]
    public async Task RepeatedPreAudioFailureEndsLineAfterTwoAttempts()
    {
        var attempts = 0;
        var client = new RecordingClient();
        await using var worker = Worker(() => new FakeSynth((_, text, _) =>
        {
            if (text == "Ready.") return Task.FromResult(Result());
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("unavailable");
        }), client);
        worker.Enqueue(Call("goal"));
        await client.WaitForAsync("audioEnd", "goal");
        Assert.Equal(2, attempts);
        Assert.DoesNotContain(client.Messages, message => message.Method == "audio");
        Assert.DoesNotContain(client.Messages, message => message.Method == "captions");
    }

    [Fact]
    public async Task PartialAudioIsNotReplayedAndNextLineStillRuns()
    {
        var calls = new ConcurrentQueue<string>();
        var client = new RecordingClient();
        await using var worker = Worker(() => new FakeSynth((synth, text, _) =>
        {
            if (text == "Ready.") return Task.FromResult(Result());
            calls.Enqueue(text);
            synth.Emit();
            if (text == "partial") throw new InvalidOperationException("interrupted");
            return Task.FromResult(Result());
        }), client);
        worker.Enqueue(Call("partial"));
        worker.Enqueue(Call("next"));
        await client.WaitForAsync("captions", "next");
        Assert.Equal(["partial", "next"], calls.ToArray());
        Assert.Single(client.Messages, message => message.Method == "audioEnd" && Equals(message.Arguments[0], "partial"));
        Assert.DoesNotContain(client.Messages, message => message.Method == "captions" && Equals(message.Arguments[0], "partial"));
    }

    private static SpeechWorker Worker(Func<IStreamingSynthesizer> factory, RecordingClient client) =>
        new(new BroadcastOptions { Channels = ["en-GB:AudioDescription"] }.ChannelSpecs().First(), factory, client, new LatencyStats(), NullLogger.Instance);

    private static Utterance Call(string id) => new(id,
        new UtteranceIntent(id, "match", IntentKind.Goal, "goal", new Dictionary<string, Slot>(),
            Priority.P0, 1, 0, 1000), "en-GB-ad", id, 0, 1000, UtteranceStatus.Done);

    private static SynthesisResult Result() => new(new byte[480], TimeSpan.FromMilliseconds(10), [], TimeSpan.Zero, TimeSpan.Zero);

    private sealed class FakeSynth(Func<FakeSynth, string, CancellationToken, Task<SynthesisResult>> speak) : IStreamingSynthesizer
    {
        public event Action<byte[]>? AudioChunk;
        public bool Disposed { get; private set; }
        public void Emit() => AudioChunk?.Invoke(new byte[480]);
        public Task<SynthesisResult> SpeakAsync(string text, CancellationToken cancellationToken = default) => speak(this, text, cancellationToken);
        public void Dispose() => Disposed = true;
    }

    private sealed record Message(string Method, object?[] Arguments);

    private sealed class RecordingClient : IClientProxy
    {
        public ConcurrentQueue<Message> Messages { get; } = new();
        private readonly Channel<Message> _received = Channel.CreateUnbounded<Message>();

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            var message = new Message(method, args);
            Messages.Enqueue(message);
            _received.Writer.TryWrite(message);
            return Task.CompletedTask;
        }

        public async Task WaitForAsync(string method, string id)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            while (await _received.Reader.ReadAsync(timeout.Token) is { } message)
            {
                if (message.Method == method && Equals(message.Arguments[0], id)) return;
            }
        }
    }
}