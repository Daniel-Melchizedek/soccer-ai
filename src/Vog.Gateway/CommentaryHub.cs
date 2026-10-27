using Microsoft.AspNetCore.SignalR;

namespace Vog.Gateway;

/// <summary>
/// Listeners join one channel (language × mode) and receive its utterances, audio, captions and
/// match state, and may ask the match companion questions by voice or text. Anonymous by design:
/// the listener app holds no personal data server-side, and question audio is never stored.
/// </summary>
public sealed class CommentaryHub(
    ChannelDirectory directory,
    LiveBroadcastService broadcast,
    CompanionRelay companion,
    ReadoutService readouts,
    LatencyStats stats,
    ILogger<CommentaryHub> log) : Hub
{
    public async Task<JoinResult> Join(string lang, string mode, bool textOnly = false)
    {
        var connectionId = Context.ConnectionId;
        log.LogInformation("Listener join requested. Connection {ConnectionId}, language {Language}, mode {Mode}, text-only {TextOnly}", connectionId, lang, mode, textOnly);

        try
        {
            var channel = directory.Find(lang, mode) ?? throw new HubException($"No channel for {lang} / {mode}.");
            if (directory.Join(connectionId, channel, textOnly) is { } previous)
            {
                var previousGroup = previous.TextOnly ? $"{previous.ChannelId}:text" : previous.ChannelId;
                log.LogDebug("Removing listener {ConnectionId} from previous group {Group}", connectionId, previousGroup);
                await Groups.RemoveFromGroupAsync(connectionId, previousGroup);
            }

            var group = textOnly ? $"{channel.Id}:text" : channel.Id;
            log.LogDebug("Adding listener {ConnectionId} to group {Group}", connectionId, group);
            await Groups.AddToGroupAsync(connectionId, group);
            await broadcast.StartMatchAsync(Context.ConnectionAborted);
            if (!textOnly)
            {
                log.LogDebug("Preparing speech and readout services for channel {Channel}", channel.Id);
                broadcast.Prepare(channel);
                readouts.Prepare(channel);
            }

            var info = broadcast.Info;
            log.LogInformation("Listener {ConnectionId} joined channel {Channel}", connectionId, channel.Id);
            return new JoinResult(
                channel.Id,
                info?.Home.Nickname ?? string.Empty,
                info?.Home.KitColour ?? string.Empty,
                info?.Away.Nickname ?? string.Empty,
                info?.Away.KitColour ?? string.Empty);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Listener join failed. Connection {ConnectionId}, language {Language}, mode {Mode}, text-only {TextOnly}", connectionId, lang, mode, textOnly);
            throw;
        }
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

    public Task Readout(string kind) => readouts.SendAsync(Context.ConnectionId, kind, Context.ConnectionAborted);

    public void AskStop() => companion.Stop(Context.ConnectionId);

    private string Lang => directory.ChannelOf(Context.ConnectionId)?.Lang ?? "en-GB";

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        if (exception is null)
        {
            log.LogInformation("Listener disconnected. Connection {ConnectionId}", connectionId);
        }
        else
        {
            log.LogWarning(exception, "Listener disconnected with an error. Connection {ConnectionId}", connectionId);
        }

        directory.Leave(connectionId);
        await companion.CloseAsync(connectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
