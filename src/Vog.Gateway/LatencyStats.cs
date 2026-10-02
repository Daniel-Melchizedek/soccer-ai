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
    private long _serverSkipped;
    private long _clientSkipped;

    public void RecordServerFirstChunk(double ms) => Add(_serverFirstChunkMs, ms);

    public void RecordClientPlay(double ms) => Add(_clientPlayMs, ms);

    public void RecordServerSkip() => Interlocked.Increment(ref _serverSkipped);

    public void RecordClientSkip() => Interlocked.Increment(ref _clientSkipped);

    public object Snapshot() => new
    {
        serverFirstChunk = Summary(_serverFirstChunkMs),
        clientReceiveToPlay = Summary(_clientPlayMs),
        skippedStale = new { server = Interlocked.Read(ref _serverSkipped), client = Interlocked.Read(ref _clientSkipped) },
    };

    private static void Add(ConcurrentQueue<double> queue, double value)
    {
        queue.Enqueue(value);
        while (queue.Count > Window && queue.TryDequeue(out _))
        {
        }
    }

    private static object Summary(ConcurrentQueue<double> queue)
    {
        var values = queue.ToArray().Order().ToArray();
        double At(double p) => values.Length == 0 ? 0 : values[Math.Clamp((int)Math.Ceiling(p * values.Length) - 1, 0, values.Length - 1)];
        return new { count = values.Length, p50 = Math.Round(At(0.5)), p95 = Math.Round(At(0.95)) };
    }
}
