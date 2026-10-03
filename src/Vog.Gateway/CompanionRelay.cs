using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Vog.Companion;
using Vog.Voice;

namespace Vog.Gateway;

/// <summary>
/// Relays each listener's questions to their own Voice Live conversation (the personal audio bus,
/// BUILD_PLAN.md D1) and their answers back to them alone. A conversation starts on the first
/// question and ends when the listener leaves. Hub calls only queue work, so a slow start never
/// holds up the listener's other messages.
/// </summary>
public sealed class CompanionRelay(
    IHubContext<CommentaryHub> hub,
    LatencyStats stats,
    IOptions<AzureAiOptions> azure,
    ILogger<CompanionRelay> log) : IAsyncDisposable
{
    /// <summary>Questions per connection, which bounds what one listener can cost.</summary>
    public const int MaxQuestions = 50;

    private readonly IHubContext<CommentaryHub> _hub = hub;
    private readonly LatencyStats _stats = stats;
    private readonly ILogger _log = log;
    private readonly ConcurrentDictionary<string, Conversation> _conversations = new();
    private readonly Uri _endpoint = azure.Value.VoiceLiveEndpoint!;
    private readonly string _project = azure.Value.ProjectName;

    public void Begin(string connectionId, string lang) => For(connectionId, lang).Ask(s => s.BeginTurnAsync(), question: true);

    public void Audio(string connectionId, byte[] pcm) => Existing(connectionId)?.Post(s => s.AppendAudioAsync(pcm));

    public void End(string connectionId) => Existing(connectionId)?.Post(s => s.EndTurnAsync(), startsAnswer: true);

    public void AskText(string connectionId, string lang, string text) => For(connectionId, lang).Ask(s => s.AskTextAsync(text), question: true, startsAnswer: true);

    public void Stop(string connectionId) => Existing(connectionId)?.Post(s => s.StopAnswerAsync());

    public async Task CloseAsync(string connectionId)
    {
        if (_conversations.TryRemove(connectionId, out var conversation))
        {
            await conversation.DisposeAsync();
        }
    }

    private Conversation? Existing(string connectionId) => _conversations.GetValueOrDefault(connectionId);

    private Conversation For(string connectionId, string lang)
    {
        while (true)
        {
            var current = _conversations.GetOrAdd(connectionId, _ => new Conversation(this, connectionId, lang));
            if (current.Lang == lang && !current.Closed)
            {
                return current;
            }

            // A new language (the listener switched channel) or a dropped session: start afresh.
            if (_conversations.TryRemove(new KeyValuePair<string, Conversation>(connectionId, current)))
            {
                _ = current.DisposeAsync().AsTask();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _conversations.Keys)
        {
            await CloseAsync(id);
        }
    }

    private sealed class Conversation : IAsyncDisposable
    {
        private readonly CompanionRelay _relay;
        private readonly IClientProxy _client;
        private readonly Channel<(Func<CompanionSession, Task> Work, bool StartsAnswer)> _commands = Channel.CreateUnbounded<(Func<CompanionSession, Task>, bool)>(new UnboundedChannelOptions { SingleReader = true });
        private readonly CancellationTokenSource _stop = new();
        private readonly Task _run;
        private int _questions;
        private long _askedAt;
        private int _answerId;
        private int _seq;
        private bool _answerOpen;

        public Conversation(CompanionRelay relay, string connectionId, string lang)
        {
            _relay = relay;
            _client = relay._hub.Clients.Client(connectionId);
            Lang = lang;
            _run = Task.Run(() => RunAsync(_stop.Token));
        }

        public string Lang { get; }

        public bool Closed { get; private set; }

        public void Ask(Func<CompanionSession, Task> work, bool question, bool startsAnswer = false)
        {
            if (question && Interlocked.Increment(ref _questions) > MaxQuestions)
            {
                _ = _client.SendAsync("answerError", $"That's the limit of {MaxQuestions} questions for this visit. Reload the page to ask more.");
                return;
            }

            Post(work, startsAnswer);
        }

        public void Post(Func<CompanionSession, Task> work, bool startsAnswer = false) => _commands.Writer.TryWrite((work, startsAnswer));

        private async Task RunAsync(CancellationToken ct)
        {
            CompanionSession? session = null;
            var started = Stopwatch.GetTimestamp();
            var options = new CompanionOptions(Lang, _relay._project);
            for (var attempt = 1; session is null; attempt++)
            {
                try
                {
                    session = await CompanionSession.StartAsync(_relay._endpoint, AzureCredentials.Shared, options, ct);
                    _relay._log.LogInformation("Companion session ready (Foundry agent {Agent}) in {Ms:0} ms", options.AgentName, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                }
                catch (Exception ex) when (ex is not OperationCanceledException && attempt < 2)
                {
                    // Connection set-up occasionally fails on busy networks; one retry hides it from the fan.
                    _relay._log.LogInformation("Companion session didn't start ({Error}); retrying", ex.Message);
                    await Task.Delay(500, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _relay._log.LogWarning(ex, "Companion session failed to start");
                    Closed = true;
                    await _client.SendAsync("answerError", "The match companion isn't available right now. The commentary carries on.", CancellationToken.None);
                    return;
                }
            }

            await using (session)
            {
                var forward = ForwardAsync(session, ct);
                try
                {
                    await foreach (var (work, startsAnswer) in _commands.Reader.ReadAllAsync(ct))
                    {
                        if (startsAnswer)
                        {
                            Interlocked.Exchange(ref _askedAt, Stopwatch.GetTimestamp());
                        }

                        await work(session);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _relay._log.LogWarning(ex, "Companion command failed");
                    await _client.SendAsync("answerError", "Sorry, that question didn't get through. Please ask again.", CancellationToken.None);
                }
                finally
                {
                    Closed = true;
                }

                await forward;
            }
        }

        /// <summary>Sends the session's events to this listener only, framing each answer with start and end.</summary>
        private async Task ForwardAsync(CompanionSession session, CancellationToken ct)
        {
            try
            {
                await foreach (var e in session.Events.ReadAllAsync(ct))
                {
                    switch (e)
                    {
                        case Heard heard:
                            await _client.SendAsync("heard", heard.Text, ct);
                            break;

                        case AnswerAudio audio:
                            await OpenAnswerAsync(ct);

                            // The answer's text usually opens it, so time the first audio on its own.
                            var askedAt = Interlocked.Exchange(ref _askedAt, 0);
                            if (askedAt > 0)
                            {
                                _relay._stats.RecordCompanionFirstAudio(Stopwatch.GetElapsedTime(askedAt).TotalMilliseconds);
                            }

                            await _client.SendAsync("answerAudio", _answerId, _seq++, audio.Pcm, ct);
                            break;

                        case AnswerText text:
                            await OpenAnswerAsync(ct);
                            await _client.SendAsync("answerText", _answerId, text.Delta, ct);
                            break;

                        case AnswerFinished done:
                            _relay._stats.RecordCompanionAnswer(done.InputTokens ?? 0, done.OutputTokens ?? 0);
                            await CloseAnswerAsync(done.Status, ct);
                            break;

                        case CompanionFailed failed:
                            _relay._log.LogWarning("Companion: {Message}", failed.Message);
                            await CloseAnswerAsync("failed", ct);
                            await _client.SendAsync("answerError", failed.ForListener ? failed.Message : "Sorry, I couldn't answer that. Please ask again.", ct);
                            break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                // The service closed the session (time limit, network): the next question opens a new one.
                Closed = true;
                _commands.Writer.TryComplete();
            }
        }

        private async Task OpenAnswerAsync(CancellationToken ct)
        {
            if (!_answerOpen)
            {
                _answerOpen = true;
                _seq = 0;
                await _client.SendAsync("answerStart", ++_answerId, ct);
            }
        }

        private async Task CloseAnswerAsync(string status, CancellationToken ct)
        {
            if (_answerOpen)
            {
                _answerOpen = false;
                await _client.SendAsync("answerEnd", _answerId, status, ct);
            }
        }

        public async ValueTask DisposeAsync()
        {
            Closed = true;
            _commands.Writer.TryComplete();
            await _stop.CancelAsync();
            try
            {
                await _run;
            }
            catch (OperationCanceledException)
            {
            }

            _stop.Dispose();
        }
    }
}
