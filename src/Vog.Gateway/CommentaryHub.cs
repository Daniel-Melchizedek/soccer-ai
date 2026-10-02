using Microsoft.AspNetCore.SignalR;

namespace Vog.Gateway;

/// <summary>
/// Listeners join one channel (language × mode) and receive its utterances, audio, captions and
/// match state. Anonymous by design: the listener app holds no personal data server-side.
/// </summary>
public sealed class CommentaryHub(ChannelDirectory directory, LiveBroadcastService broadcast, LatencyStats stats) : Hub
{
    public async Task<JoinResult> Join(string lang, string mode)
    {
        var channel = directory.Find(lang, mode) ?? throw new HubException($"No channel for {lang} / {mode}.");
        if (directory.Join(Context.ConnectionId, channel) is { } previous && previous != channel.Id)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previous);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, channel.Id);
        broadcast.Prepare(channel);

        var info = broadcast.Info;
        return new JoinResult(
            channel.Id,
            info?.Home.Nickname ?? string.Empty,
            info?.Home.KitColour ?? string.Empty,
            info?.Away.Nickname ?? string.Empty,
            info?.Away.KitColour ?? string.Empty,
            broadcast.SpeechEnabled);
    }

    /// <summary>The client reports how long after receiving an utterance its audio started playing.</summary>
    public void Played(string utteranceId, double receiveToPlayMs)
    {
        if (receiveToPlayMs is >= 0 and < 60_000)
        {
            stats.RecordClientPlay(receiveToPlayMs);
        }
    }

    /// <summary>The client skipped a line that would have started too late to be useful.</summary>
    public void Skipped(string utteranceId) => stats.RecordClientSkip();

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        directory.Leave(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
