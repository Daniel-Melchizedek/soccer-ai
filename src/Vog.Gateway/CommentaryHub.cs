using Microsoft.AspNetCore.SignalR;

namespace Vog.Gateway;

/// <summary>
/// Listeners join one channel (language × mode) and receive its utterances, audio, captions and
/// match state, and may ask the match companion questions by voice or text. Anonymous by design:
/// the listener app holds no personal data server-side, and question audio is never stored.
/// </summary>
public sealed class CommentaryHub(ChannelDirectory directory, LiveBroadcastService broadcast, CompanionRelay companion, LatencyStats stats) : Hub
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
            info?.Away.KitColour ?? string.Empty);
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

    /// <summary>Push-to-talk pressed: a spoken question follows as 24 kHz 16-bit PCM chunks.</summary>
    public void AskBegin() => companion.Begin(Context.ConnectionId, Lang);

    /// <summary>A chunk of the question, base64 (the JSON protocol has no binary type).</summary>
    public void AskAudio(string pcmBase64)
    {
        var bytes = new byte[pcmBase64.Length * 3 / 4];
        if (Convert.TryFromBase64String(pcmBase64, bytes, out var written))
        {
            companion.Audio(Context.ConnectionId, bytes[..written]);
        }
    }

    /// <summary>Push-to-talk released: answer the question.</summary>
    public void AskEnd() => companion.End(Context.ConnectionId);

    public void AskText(string question) => companion.AskText(Context.ConnectionId, Lang, question);

    public void AskStop() => companion.Stop(Context.ConnectionId);

    private string Lang => directory.ChannelOf(Context.ConnectionId)?.Lang ?? "en-GB";

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        directory.Leave(Context.ConnectionId);
        await companion.CloseAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
