using System.Diagnostics;
using System.Text;
using Vog.Companion;
using Vog.Voice;

namespace Vog.Replay;

/// <summary>
/// Talks to the match companion directly, without a gateway: a Voice Live agent-mode session with the
/// Foundry agent vog-match-companion-&lt;lang&gt;, whose MCP tools read the deployed gateway's match.
/// A spoken question is synthesized and streamed as push-to-talk audio, as in the browser.
/// </summary>
internal static class CompanionCheck
{
    public static async Task<int> RunAsync(Options options)
    {
        DotEnv.Load();
        var endpoint = Required("VOG_AI_SERVICES_ENDPOINT");
        var project = Required("VOG_FOUNDRY_PROJECT_NAME");
        var lang = options.String("lang", "en-GB");
        var spoken = options.Flag("say");
        var question = spoken ? options.String("say", "Who has the ball?") : options.String("text", "What's the score?");

        var started = Stopwatch.GetTimestamp();
        await using var session = await CompanionSession.StartAsync(new Uri(endpoint), AzureCredentials.Shared, new CompanionOptions(lang, project), CancellationToken.None);
        Console.WriteLine($"Session with vog-match-companion-{lang.ToLowerInvariant()} ready in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:0} ms");

        long asked;
        if (spoken)
        {
            var fan = lang.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "es-ES-ElviraNeural" : "en-GB-LibbyNeural";
            using var tts = new NeuralSynthesizer(new Uri(Required("VOG_AI_ENDPOINT")), AzureCredentials.Shared, new VoiceProfile(lang, fan, "+0%"));
            var pcm = (await tts.SpeakAsync(question)).Pcm;
            await session.BeginTurnAsync();
            for (var offset = 0; offset < pcm.Length; offset += 4800)
            {
                await session.AppendAudioAsync(pcm[offset..Math.Min(pcm.Length, offset + 4800)]);
                await Task.Delay(100);
            }

            asked = Stopwatch.GetTimestamp();
            await session.EndTurnAsync();
        }
        else
        {
            asked = Stopwatch.GetTimestamp();
            await session.AskTextAsync(question);
        }

        Console.WriteLine($"{(spoken ? "Says" : "Types")}: {question}");
        var answer = new StringBuilder();
        double? firstAudio = null;
        long audioBytes = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await foreach (var e in session.Events.ReadAllAsync(timeout.Token))
        {
            switch (e)
            {
                case Heard heard:
                    Console.WriteLine($"  heard:  {heard.Text}");
                    break;
                case AnswerAudio audio:
                    firstAudio ??= Stopwatch.GetElapsedTime(asked).TotalMilliseconds;
                    audioBytes += audio.Pcm.Length;
                    break;
                case AnswerText text:
                    answer.Append(text.Delta);
                    break;
                case CompanionFailed failed:
                    Console.WriteLine($"  failed: {failed.Message}");
                    break;
                case AnswerFinished done:
                    Console.WriteLine($"  answer: {answer}");
                    Console.WriteLine($"  {done.Status} · first audio {firstAudio:0} ms after the question · {audioBytes / 48 / 1000.0:0.0} s of speech · tokens in {done.InputTokens} out {done.OutputTokens}");
                    return answer.Length > 0 && audioBytes > 0 ? 0 : 1;
            }
        }

        return 1;
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is not set. Run infra/provision.ps1, which writes it to .env.local.");
}
