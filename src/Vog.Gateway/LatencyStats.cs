using System.Collections.Concurrent;

namespace Vog.Gateway;

/// <summary>
/// Rolling latency measurements for the live audio path:
/// server = booth start → first audio chunk sent; client = message received → playback started.
/// Lines skipped as stale (see <see cref="Staleness"/>) are counted, so skipping can't hide lateness.
/// </summary>
public sealed class LatencyStats
{
    private const int Window = 500;

    private readonly ConcurrentQueue<double> _serverFirstChunkMs = new();
    private readonly ConcurrentQueue<double> _clientPlayMs = new();
    private readonly ConcurrentQueue<double> _companionFirstAudioMs = new();
    private long _serverSkipped;
    private long _clientSkipped;
    private long _answers;
    private long _inputTokens;
    private long _outputTokens;
    private long _toolCalls;

    public void RecordServerFirstChunk(double ms) => Add(_serverFirstChunkMs, ms);

    public void RecordClientPlay(double ms) => Add(_clientPlayMs, ms);

    public void RecordServerSkip() => Interlocked.Increment(ref _serverSkipped);

    public void RecordClientSkip() => Interlocked.Increment(ref _clientSkipped);

    /// <summary>Question finished (button released, or text sent) → first answer audio sent.</summary>
    public void RecordCompanionFirstAudio(double ms) => Add(_companionFirstAudioMs, ms);

    public void RecordCompanionAnswer(int inputTokens, int outputTokens)
    {
        Interlocked.Increment(ref _answers);
        Interlocked.Add(ref _inputTokens, inputTokens);
        Interlocked.Add(ref _outputTokens, outputTokens);
    }

    /// <summary>A Foundry agent called one of the match tools over MCP.</summary>
    public void RecordToolCall() => Interlocked.Increment(ref _toolCalls);

    public object Snapshot() => new
    {
        serverFirstChunk = Summary(_serverFirstChunkMs),
        clientReceiveToPlay = Summary(_clientPlayMs),
        skippedStale = new { server = Interlocked.Read(ref _serverSkipped), client = Interlocked.Read(ref _clientSkipped) },
        companion = new
        {
            questionToFirstAudio = Summary(_companionFirstAudioMs),
            answers = Interlocked.Read(ref _answers),
            inputTokens = Interlocked.Read(ref _inputTokens),
            outputTokens = Interlocked.Read(ref _outputTokens),
            toolCalls = Interlocked.Read(ref _toolCalls),
        },
    };

    private static void Add(ConcurrentQueue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > Window)
        {
            queue.TryDequeue(out _);
        }
    }

    private static object Summary(ConcurrentQueue<double> queue)
    {
        var values = queue.ToArray().Order().ToArray();
        double At(double p) => values.Length == 0 ? 0 : values[Math.Clamp((int)Math.Ceiling(p * values.Length) - 1, 0, values.Length - 1)];
        return new { count = values.Length, p50 = Math.Round(At(0.5)), p95 = Math.Round(At(0.95)) };
    }
}
