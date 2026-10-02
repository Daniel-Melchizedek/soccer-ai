using Vog.Booth;
using Vog.Contracts;
using Vog.Domain;
using Vog.Pipeline;
using Vog.Voice;

namespace Vog.Replay;

/// <summary>
/// Spike S1 (BUILD_PLAN.md §16): renders a window of the golden match through the booth and real
/// neural TTS into a WAV plus word-timed WebVTT captions, and reports time-to-first-byte and how well
/// the booth's duration estimates match real speech (which calibrates <see cref="ProducerPolicy.WordsPerSecond"/>).
/// </summary>
internal static class Speak
{
    /// <summary>The stats engine holds passes and shots this long to measure ball speed.</summary>
    private const long StatsHoldMs = 300;

    public static async Task<int> RunAsync(Options options)
    {
        var envFile = DotEnv.Load();
        var endpoint = Environment.GetEnvironmentVariable("VOG_AI_ENDPOINT")
            ?? throw new InvalidOperationException("VOG_AI_ENDPOINT is not set. Run infra/provision.ps1 first; it writes .env.local.");

        var seed = options.ULong("seed", GoldenMatch.DefaultMatchSeed);
        var lang = options.String("lang", "en-GB");
        var mode = Commentary.ParseMode(options.String("mode", "ad"));
        var from = options.Int("from", 0);
        var minutes = options.Int("minutes", 2);
        var outDir = Path.GetFullPath(options.String("out", Path.Combine("out", "audio")));
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"Settings from {envFile ?? "environment"} · {lang} · {mode} · match minutes {from}–{from + minutes}");

        // 1. Schedule the window with the booth (virtual time, deterministic).
        var channel = new ChannelSpec(lang, mode);
        var starts = new Dictionary<string, (int Period, long ClockMs)>();
        var played = new List<Utterance>();
        OfflineMatchRun.Run(GoldenMatch.Setup(seed), [channel], e =>
        {
            var minute = e.Frame.MatchClockMs / 60_000;
            if (e.Event.Kind == BoothEventKind.Started && minute >= from && minute < from + minutes)
            {
                starts[e.Event.Utterance.UtteranceId] = (e.Frame.Period, e.Frame.MatchClockMs);
            }
            else if (e.Event.Kind == BoothEventKind.Finished && starts.ContainsKey(e.Event.Utterance.UtteranceId))
            {
                played.Add(e.Event.Utterance);
            }
        }, stopAfterMinute: from + minutes + 1);

        if (played.Count == 0)
        {
            Console.WriteLine("Nothing scheduled in that window.");
            return 1;
        }

        // 2. Speak each utterance and lay it on the timeline.
        using var tts = new NeuralSynthesizer(new Uri(endpoint), AzureCredentials.Shared, VoiceCatalog.For(lang, mode));
        await tts.SpeakAsync(lang.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? "Listo." : "Ready."); // warm-up, not measured

        var origin = played[0].StartPtsMs;
        var track = new List<byte>();
        var cues = new List<CaptionCue>();
        var firstByte = new List<double>();
        var ratios = new List<double>();
        var addedIfOnStart = new List<double>();
        var addedIfOnEnqueue = new List<double>();
        long cursor = 0;
        var maxDrift = 0L;
        var overruns = 0;

        foreach (var u in played)
        {
            var result = await tts.SpeakAsync(u.Text);
            var scheduled = u.StartPtsMs - origin;
            var start = Math.Max(scheduled, cursor);
            maxDrift = Math.Max(maxDrift, start - scheduled);

            // A preempted utterance stops where the booth cut it, at a word boundary.
            var pcm = u.Status == UtteranceStatus.Preempted
                ? PcmEdit.CutAtWordBoundary(result.Pcm, result.Words, u.DurationMs)
                : result.Pcm;

            // Synthesizing when the intent is queued (not when it airs) hides first-byte latency
            // whenever the item waited in the queue for longer than the service takes to respond.
            if (u.Intent.Source == IntentSource.Template)
            {
                var queuedAt = u.Intent.PtsMs + StatsHoldMs;
                addedIfOnStart.Add(result.FirstByteLatency.TotalMilliseconds);
                addedIfOnEnqueue.Add(Math.Max(0, queuedAt + result.FirstByteLatency.TotalMilliseconds - u.StartPtsMs));
            }

            // Duration from the bytes themselves: the reported AudioDuration can differ slightly.
            var spokenMs = Wav.MillisecondsFor(pcm.Length);
            if (u.Status != UtteranceStatus.Preempted)
            {
                ratios.Add((double)spokenMs / Math.Max(1, u.DurationMs));
                if (spokenMs > u.DurationMs + 300)
                {
                    overruns++;
                }
            }

            var startByte = Math.Max(Wav.BytesFor(start), track.Count);
            track.AddRange(new byte[startByte - track.Count]);
            track.AddRange(pcm);
            cursor = Wav.MillisecondsFor(track.Count);
            firstByte.Add(result.FirstByteLatency.TotalMilliseconds);

            var words = result.Words.Where(w => w.Offset.TotalMilliseconds < spokenMs).ToList();
            cues.AddRange(Captions.FromWords(start, words, u.Text, spokenMs));

            var (period, clock) = starts[u.UtteranceId];
            Console.WriteLine($"[{MatchClock.Display(period, clock),6}] {u.Text}  (first byte {result.FirstByteLatency.TotalMilliseconds:0} ms · spoken {spokenMs} ms / scheduled {u.DurationMs} ms{(u.Status == UtteranceStatus.Preempted ? " · cut" : string.Empty)})");
        }

        var name = $"{seed}-{lang}-{mode.ToString().ToLowerInvariant()}-{from:D2}m";
        var wavPath = Path.Combine(outDir, name + ".wav");
        var vttPath = Path.Combine(outDir, name + ".vtt");
        Wav.Write(wavPath, track.ToArray());
        await File.WriteAllTextAsync(vttPath, Captions.ToWebVtt(cues));

        static double P(List<double> v, double p) => v.Count == 0 ? 0 : v.Order().ElementAt(Math.Clamp((int)Math.Ceiling(p * v.Count) - 1, 0, v.Count - 1));
        Console.WriteLine();
        Console.WriteLine($"{played.Count} utterances · {cursor / 1000.0:0.0} s of audio · {cues.Count} caption cues");
        Console.WriteLine($"TTS first byte: p50 {P(firstByte, 0.5):0} ms · p95 {P(firstByte, 0.95):0} ms (target ≤ 250 ms on the live path)");
        Console.WriteLine($"Added latency for event calls: synthesize on start p50 {P(addedIfOnStart, 0.5):0} ms · synthesize on enqueue p50 {P(addedIfOnEnqueue, 0.5):0} ms, p95 {P(addedIfOnEnqueue, 0.95):0} ms");
        Console.WriteLine($"Spoken ÷ scheduled duration: median {P(ratios, 0.5):0.00} · p95 {P(ratios, 0.95):0.00} · {overruns} overrun(s) > 300 ms");
        Console.WriteLine($"Max timeline drift caused by overruns: {maxDrift} ms");
        Console.WriteLine($"Wrote {wavPath}");
        Console.WriteLine($"Wrote {vttPath}");
        return 0;
    }
}
