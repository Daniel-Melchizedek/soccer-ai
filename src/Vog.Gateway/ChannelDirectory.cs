using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Vog.Pipeline;

namespace Vog.Gateway;

/// <summary>
/// Which listener is on which channel. Audio is synthesized only for channels that have
/// listeners, so cost scales with active channels, not with the channels on offer.
/// </summary>
public sealed class ChannelDirectory(IOptions<BroadcastOptions> options)
{
    private readonly ConcurrentDictionary<string, string> _channelByConnection = new();

    public IReadOnlyList<ChannelSpec> Available { get; } = options.Value.ChannelSpecs();

    public ChannelSpec? Find(string lang, string mode) =>
        Available.FirstOrDefault(c => c.Lang.Equals(lang, StringComparison.OrdinalIgnoreCase) && c.Mode.ToString().Equals(mode, StringComparison.OrdinalIgnoreCase));

    public string? Join(string connectionId, ChannelSpec channel)
    {
        string? previous = null;
        _channelByConnection.AddOrUpdate(connectionId, channel.Id, (_, old) =>
        {
            previous = old;
            return channel.Id;
        });
        return previous;
    }

    public string? Leave(string connectionId) =>
        _channelByConnection.TryRemove(connectionId, out var channelId) ? channelId : null;

    public ChannelSpec? ChannelOf(string connectionId) =>
        _channelByConnection.TryGetValue(connectionId, out var channelId) ? Available.FirstOrDefault(c => c.Id == channelId) : null;

    public bool HasListeners(string channelId) => _channelByConnection.Values.Contains(channelId);

    public IReadOnlyDictionary<string, int> ListenerCounts() =>
        _channelByConnection.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());
}
