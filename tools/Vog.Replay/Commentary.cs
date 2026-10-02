using Vog.Booth;
using Vog.Contracts;
using Vog.Domain;
using Vog.Pipeline;

namespace Vog.Replay;

/// <summary>
/// Plays a golden match through the HOT path and prints the commentary timeline as a listener
/// would hear it, followed by the channel's accessibility coverage report.
/// </summary>
internal static class Commentary
{
    public static int Run(Options options)
    {
        var seed = options.ULong("seed", GoldenMatch.DefaultMatchSeed);
        var lang = options.String("lang", "en-GB");
        var mode = ParseMode(options.String("mode", "ad"));
        var from = options.Int("from", 0);
        var to = options.Int("to", 200);
        var quiet = options.Flag("quiet");
        var verbose = options.Flag("verbose");
        var stop = options.Flag("minutes") ? options.Int("minutes", 95) : (int?)null;

        var setup = GoldenMatch.Setup(seed);
        var channel = new ChannelSpec(lang, mode);
        Console.WriteLine($"{setup.Home.Name} ({setup.Home.Nickname}) v {setup.Away.Name} ({setup.Away.Nickname}) · seed {seed} · {channel.Id}");
        Console.WriteLine();

        var result = OfflineMatchRun.Run(setup, [channel], e =>
        {
            if (quiet || e.Event.Kind != BoothEventKind.Started)
            {
                return;
            }

            var minute = (int)(e.Frame.MatchClockMs / 60_000);
            if (minute < from || minute > to)
            {
                return;
            }

            var u = e.Event.Utterance;
            var clock = MatchClock.Display(e.Frame.Period, e.Frame.MatchClockMs);
            var tag = verbose ? $" ({u.Intent.Priority} {u.Intent.TemplateKey}, +{u.StartPtsMs - u.Intent.PtsMs} ms)" : string.Empty;
            Console.WriteLine($"[{clock,6}] {u.Text}{tag}");
        }, stop);

        var report = result.Channels[0].Coverage;
        var policy = result.Channels[0].Booth.Policy;

        Console.WriteLine();
        Console.WriteLine($"Final score: {result.Info.Home.Nickname} {result.HomeScore}, {result.Info.Away.Nickname} {result.AwayScore}");
        Console.WriteLine($"Ball in play {report.InPlayMinutes:0.0} min · {report.Utterances} utterances · {report.SpeechMinutes:0.0} min of speech · {report.Preempted} preempted");
        Console.WriteLine($"Drops: {string.Join(", ", report.DropsByStatus.Select(kv => $"{kv.Key} {kv.Value}"))}");
        Console.WriteLine();
        Console.WriteLine($"{"Coverage check",-40} {"value",10}   target");
        var failures = 0;
        foreach (var (metric, value, target, pass) in report.Checks(policy))
        {
            failures += pass ? 0 : 1;
            Console.WriteLine($"{metric,-40} {value,10}   {target,-10} {(pass ? "ok" : "FAIL")}");
        }

        if (options.Flag("latency"))
        {
            Console.WriteLine();
            Console.WriteLine($"{"template",-28} {"count",6} {"p50 ms",8} {"p95 ms",8}");
            var timeline = result.Channels[0].Booth.Timeline.Where(x => x.Intent.Source == IntentSource.Template);
            foreach (var group in timeline.GroupBy(x => x.Intent.TemplateKey).OrderByDescending(g => g.Count()))
            {
                var latencies = group.Select(x => x.StartPtsMs - x.Intent.PtsMs).Order().ToList();
                long At(double p) => latencies[Math.Clamp((int)Math.Ceiling(p * latencies.Count) - 1, 0, latencies.Count - 1)];
                Console.WriteLine($"{group.Key,-28} {latencies.Count,6} {At(0.5),8} {At(0.95),8}");
            }

            Console.WriteLine();
            Console.WriteLine("Urgent items lost (stale P0/P1):");
            foreach (var d in result.Channels[0].Booth.Dropped.Where(d => d.Status == UtteranceStatus.DroppedStale && d.Intent.Priority <= Priority.P1))
            {
                Console.WriteLine($"  pts {d.Intent.PtsMs / 1000.0,8:0.0} s  dropped at {d.StartPtsMs / 1000.0,8:0.0} s  {d.Intent.TemplateKey}: {d.Text}");
            }

            Console.WriteLine();
            Console.WriteLine("Dropped (stale) by template:");
            foreach (var group in result.Channels[0].Booth.Dropped.Where(d => d.Status == UtteranceStatus.DroppedStale).GroupBy(d => d.Intent.TemplateKey).OrderByDescending(g => g.Count()))
            {
                Console.WriteLine($"  {group.Key,-26} {group.Count(),6}");
            }
        }

        return failures == 0 ? 0 : 1;
    }

    private static ListenerMode ParseMode(string mode) => mode.ToLowerInvariant() switch
    {
        "ad" or "audiodescription" => ListenerMode.AudioDescription,
        "commentary" => ListenerMode.Commentary,
        "calm" => ListenerMode.Calm,
        "analyst" => ListenerMode.Analyst,
        "casual" => ListenerMode.Casual,
        "kids" => ListenerMode.Kids,
        _ => throw new ArgumentException($"Unknown mode '{mode}'. Use ad, commentary, calm, analyst, casual or kids."),
    };
}
