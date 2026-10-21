using System.Diagnostics;
using System.Threading.Channels;
using Vog.Contracts;

namespace Vog.Gateway;

public sealed record PendingSpeech(Utterance Utterance, long StartedAt);

public sealed class PendingSpeechQueue
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly List<PendingSpeech> _pending = [];
    private readonly Channel<bool> _available = Channel.CreateBounded<bool>(1);
    private bool _completed;

    public PendingSpeechQueue(int capacity = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    public Utterance? Enqueue(Utterance utterance)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return utterance;
            }

            Utterance? dropped = null;
            if (_pending.Count == _capacity)
            {
                var victim = 0;
                for (var index = 1; index < _pending.Count; index++)
                {
                    if (_pending[index].Utterance.Intent.Priority > _pending[victim].Utterance.Intent.Priority)
                    {
                        victim = index;
                    }
                }

                if (utterance.Intent.Priority > _pending[victim].Utterance.Intent.Priority)
                {
                    return utterance;
                }

                dropped = _pending[victim].Utterance;
                _pending.RemoveAt(victim);
            }

            _pending.Add(new PendingSpeech(utterance, Stopwatch.GetTimestamp()));
            _available.Writer.TryWrite(true);
            return dropped;
        }
    }

    public async ValueTask<PendingSpeech?> DequeueAsync(CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_pending.Count > 0)
                {
                    var next = _pending[0];
                    _pending.RemoveAt(0);
                    return next;
                }

                if (_completed)
                {
                    return null;
                }
            }

            await _available.Reader.ReadAsync(ct);
        }
    }

    public void Complete()
    {
        lock (_gate)
        {
            _completed = true;
            _available.Writer.TryWrite(true);
        }
    }
}