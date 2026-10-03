using System.Threading.Channels;
using Azure.AI.VoiceLive;
using Azure.Core;
using Vog.Voice;

namespace Vog.Companion;

/// <summary>What happened in a companion conversation, in order, for the relay to forward.</summary>
public abstract record CompanionEvent;

/// <summary>What the service heard the fan say (push-to-talk), shown back so they can check it.</summary>
public sealed record Heard(string Text) : CompanionEvent;

public sealed record AnswerAudio(byte[] Pcm) : CompanionEvent;

public sealed record AnswerText(string Delta) : CompanionEvent;

public sealed record AnswerFinished(string Status, int? InputTokens, int? OutputTokens) : CompanionEvent;

/// <param name="ForListener">False for the service's own wording, which is logged rather than shown.</param>
public sealed record CompanionFailed(string Message, bool ForListener = true) : CompanionEvent;

/// <summary>Which Foundry agent answers, and in which voice.</summary>
public sealed record CompanionOptions(string Lang, string ProjectName)
{
    /// <summary>Registered per language by tools/Vog.FoundrySetup from foundry/agents/vog-match-companion.agent.yaml.</summary>
    public string AgentName => $"vog-match-companion-{Lang.ToLowerInvariant()}";

    public string Voice => VoiceCatalog.Companion(Lang);
}

/// <summary>
/// One fan's conversation with the match companion: Azure Voice Live in agent mode, connected to the
/// Foundry agent vog-match-companion-&lt;lang&gt;. Voice Live hears the question (push-to-talk or typed)
/// and speaks the answer in 24 kHz PCM, like the commentary; the agent's own instructions, model,
/// guardrail and MCP tools (the gateway's live match) produce it. Runs server-side, so the browser
/// holds no credentials.
/// </summary>
public sealed class CompanionSession : IAsyncDisposable
{
    /// <summary>100 ms of 24 kHz 16-bit audio: the service rejects a shorter question.</summary>
    public const int MinTurnBytes = 4800;

    /// <summary>A question longer than 20 s is cut there, which bounds what one turn can cost.</summary>
    public const int MaxTurnBytes = 960_000;

    public const int MaxTextLength = 300;

    private readonly VoiceLiveSession _session;
    private readonly Channel<CompanionEvent> _events = Channel.CreateUnbounded<CompanionEvent>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _receive;
    private volatile bool _answering;
    private int _turnBytes;

    private CompanionSession(VoiceLiveSession session)
    {
        _session = session;
        _receive = Task.Run(() => ReceiveAsync(_stop.Token));
    }

    public ChannelReader<CompanionEvent> Events => _events.Reader;

    public static async Task<CompanionSession> StartAsync(Uri endpoint, TokenCredential credential, CompanionOptions options, CancellationToken cancellationToken)
    {
        var client = new VoiceLiveClient(endpoint, credential);
        var session = await client.StartSessionAsync(new AgentSessionConfig(options.AgentName, options.ProjectName), cancellationToken);

        // Instructions, model and tools are the agent's own; the session only sets how it sounds and listens.
        await session.ConfigureSessionAsync(new VoiceLiveSessionOptions
        {
            Voice = new AzureStandardVoice(options.Voice),
            InputAudioFormat = InputAudioFormat.Pcm16,
            OutputAudioFormat = OutputAudioFormat.Pcm16,
            TurnDetection = new NoTurnDetection(), // push-to-talk: the fan says when the question ends
            // Agent sessions take azure-speech or mai-transcribe; the SDK's MaiTranscribe1 ("mai-transcribe-1") is retired.
            InputAudioTranscription = new AudioInputTranscriptionOptions(new AudioInputTranscriptionOptionsModel("mai-transcribe")) { Language = options.Lang },
        }, cancellationToken);

        return new CompanionSession(session);
    }

    /// <summary>The fan pressed the talk button: stop any answer still playing and start a new question.</summary>
    public async Task BeginTurnAsync(CancellationToken cancellationToken = default)
    {
        await StopAnswerAsync(cancellationToken);
        await _session.ClearInputAudioAsync(cancellationToken);
        _turnBytes = 0;
    }

    public async Task AppendAudioAsync(byte[] pcm, CancellationToken cancellationToken = default)
    {
        var room = MaxTurnBytes - _turnBytes;
        if (room <= 0)
        {
            return;
        }

        var chunk = pcm.Length <= room ? pcm : pcm[..(room & ~1)];
        _turnBytes += chunk.Length;
        await _session.SendInputAudioAsync(chunk, cancellationToken);
    }

    /// <summary>The fan released the button: answer what they said.</summary>
    public async Task EndTurnAsync(CancellationToken cancellationToken = default)
    {
        if (_turnBytes < MinTurnBytes)
        {
            _events.Writer.TryWrite(new CompanionFailed("That was too short to hear. Hold the button while you ask."));
            return;
        }

        await _session.CommitInputAudioAsync(cancellationToken);
        await _session.StartResponseAsync(cancellationToken);
    }

    /// <summary>A typed question: the equal alternative for fans who can't, or would rather not, speak.</summary>
    public async Task AskTextAsync(string question, CancellationToken cancellationToken = default)
    {
        var text = question.Trim();
        if (text.Length == 0)
        {
            return;
        }

        await StopAnswerAsync(cancellationToken);
        await _session.AddItemAsync(new UserMessageItem(text.Length <= MaxTextLength ? text : text[..MaxTextLength]), cancellationToken);
        await _session.StartResponseAsync(cancellationToken);
    }

    public Task StopAnswerAsync(CancellationToken cancellationToken = default) =>
        _answering ? _session.CancelResponseAsync(cancellationToken) : Task.CompletedTask;

    private async Task ReceiveAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var update in _session.GetUpdatesAsync(ct))
            {
                switch (update)
                {
                    case SessionUpdateResponseCreated:
                        _answering = true;
                        break;

                    case SessionUpdateResponseAudioDelta audio when audio.Delta is { } delta:
                        _events.Writer.TryWrite(new AnswerAudio(delta.ToArray()));
                        break;

                    case SessionUpdateResponseAudioTranscriptDelta transcript:
                        _events.Writer.TryWrite(new AnswerText(transcript.Delta));
                        break;

                    case SessionUpdateConversationItemInputAudioTranscriptionCompleted heard:
                        _events.Writer.TryWrite(new Heard(heard.Transcript));
                        break;

                    case SessionUpdateResponseDone done:
                        _answering = false;
                        var usage = done.Response?.Usage;
                        _events.Writer.TryWrite(new AnswerFinished(done.Response?.Status?.ToString() ?? "unknown", usage?.InputTokens, usage?.OutputTokens));
                        break;

                    case SessionUpdateError error:
                        _events.Writer.TryWrite(new CompanionFailed(error.Error?.Message ?? "The companion service reported an error.", ForListener: false));
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _events.Writer.TryWrite(new CompanionFailed($"The companion connection closed: {ex.Message}", ForListener: false));
        }
        finally
        {
            _events.Writer.TryComplete();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await _receive;
        }
        catch (OperationCanceledException)
        {
        }

        await _session.DisposeAsync();
        _stop.Dispose();
    }
}
