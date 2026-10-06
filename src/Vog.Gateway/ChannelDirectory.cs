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
    private readonly ConcurrentDictionary<string, ListenerMembership> _listenerByConnection = new();

    public IReadOnlyList<ChannelSpec> Available { get; } = options.Value.ChannelSpecs();

    public ChannelSpec? Find(string lang, string mode) =>
        Available.FirstOrDefault(c => c.Lang.Equals(lang, StringComparison.OrdinalIgnoreCase) && c.Mode.ToString().Equals(mode, StringComparison.OrdinalIgnoreCase));

    public ListenerMembership? Join(string connectionId, ChannelSpec channel, bool textOnly)
    {
        ListenerMembership? previous = null;
        _listenerByConnection.AddOrUpdate(connectionId, new ListenerMembership(channel.Id, textOnly), (_, old) =>
        {
            previous = old;
            return new ListenerMembership(channel.Id, textOnly);
        });
        return previous;
    }

    public ListenerMembership? Leave(string connectionId) =>
        _listenerByConnection.TryRemove(connectionId, out var membership) ? membership : null;

    public ChannelSpec? ChannelOf(string connectionId) =>
        _listenerByConnection.TryGetValue(connectionId, out var membership) ? Available.FirstOrDefault(c => c.Id == membership.ChannelId) : null;

    public bool HasListeners(string channelId) => _listenerByConnection.Values.Any(x => x.ChannelId == channelId && !x.TextOnly);

    public bool HasTextListeners(string channelId) => _listenerByConnection.Values.Any(x => x.ChannelId == channelId && x.TextOnly);

    public bool IsTextOnly(string connectionId) => _listenerByConnection.TryGetValue(connectionId, out var membership) && membership.TextOnly;

    public bool HasAnyListeners(string channelId) => _listenerByConnection.Values.Any(x => x.ChannelId == channelId);

    public IReadOnlyDictionary<string, int> ListenerCounts() =>
        _listenerByConnection.Values.Where(x => !x.TextOnly).GroupBy(x => x.ChannelId).ToDictionary(g => g.Key, g => g.Count());
}

public sealed record ListenerMembership(string ChannelId, bool TextOnly);
