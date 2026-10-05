using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

namespace Vog.Replay;

/// <summary>
/// Spike S2 smoke test (BUILD_PLAN.md §16): joins a running gateway's channel like a browser would,
/// listens for a while, and checks the audio path end to end: every aired utterance gets its audio
/// (chunks in order, then an end marker) and its captions, and reports receive-to-first-audio latency.
/// </summary>
internal static class Listen
{
    private sealed record JoinReply(string ChannelId, string Home, string HomeKit, string Away, string AwayKit);

    private sealed record UtteranceReply(string Id, string Text, string Kind, string Priority, string? Earcon, double? Pan, string Clock, long ScheduledMs);

    private sealed record CueReply(long StartMs, long EndMs, string[] Lines);

    private sealed record StateReply(string Clock, int Period, int HomeScore, int AwayScore, bool BallInPlay);

    private sealed record PitchReply(long T, int P, double[] B, string? O, bool Hr, double[][] H, double[][] A);

    private sealed class Line(UtteranceReply utterance, long receivedAt)
    {
        public UtteranceReply Utterance { get; } = utterance;
        public long ReceivedAt { get; } = receivedAt;
        public double? FirstAudioMs { get; set; }
        public int NextSeq { get; set; }
        public bool OutOfOrder { get; set; }
        public long Bytes { get; set; }
        public long? SpokenMs { get; set; }
        public int Cues { get; set; }
        public long? CutAfterMs { get; set; }
        public bool Skipped { get; set; }
    }

    public static async Task<int> RunAsync(Options options)
    {
        var url = options.String("url", "http://localhost:5080").TrimEnd('/');
        var lang = options.String("lang", "en-GB");
        var mode = options.String("mode", "AudioDescription");
        var seconds = options.Int("seconds", 30);

        var lines = new Dictionary<string, Line>();
        var gate = new Lock();
        StateReply? lastState = null;

        await using var connection = new HubConnectionBuilder().WithUrl($"{url}/hubs/commentary").Build();
        connection.On<UtteranceReply>("utterance", u =>
        {
            lock (gate)
            {
                lines[u.Id] = new Line(u, Stopwatch.GetTimestamp());
            }

            Console.WriteLine($"  {u.Clock,7} {u.Priority} {u.Text}");
        });
        connection.On<string, int, byte[]>("audio", (id, seq, data) =>
        {
            lock (gate)
            {
                if (lines.TryGetValue(id, out var line))
                {
                    line.FirstAudioMs ??= Stopwatch.GetElapsedTime(line.ReceivedAt).TotalMilliseconds;
                    line.OutOfOrder |= seq != line.NextSeq;
                    line.NextSeq = seq + 1;
                    line.Bytes += data.Length;
                }
            }
        });
        connection.On<string, long>("audioEnd", (id, spokenMs) => Update(id, line => line.SpokenMs = spokenMs));
        connection.On<string, CueReply[]>("captions", (id, cues) => Update(id, line => line.Cues = cues.Length));
        connection.On<string, long>("cut", (id, afterMs) => Update(id, line => line.CutAfterMs = afterMs));
        connection.On<string>("skipped", id => Update(id, line => line.Skipped = true));
        connection.On<StateReply>("state", s => lastState = s);
        var pitches = new List<(long ReceivedAt, PitchReply Pitch)>();
        connection.On<PitchReply>("pitch", p =>
        {
            lock (gate)
            {
                pitches.Add((Stopwatch.GetTimestamp(), p));
            }
        });

        void Update(string id, Action<Line> change)
        {
            lock (gate)
            {
                if (lines.TryGetValue(id, out var line))
                {
                    change(line);
                }
            }
        }

        await connection.StartAsync();
        var join = await connection.InvokeAsync<JoinReply>("Join", lang, mode);
        Console.WriteLine($"Joined {join.ChannelId}: {join.Home} ({join.HomeKit}) v {join.Away} ({join.AwayKit}) · listening {seconds} s");

        await Task.Delay(TimeSpan.FromSeconds(seconds));

        // Lines still being spoken when we stop aren't judged.
        await connection.StopAsync();
        var all = lines.Values.ToList();
        var skipped = all.Count(l => l.Skipped);
        var settled = all.Where(l => !l.Skipped && (l.Cues > 0 || l.SpokenMs is not null)).ToList();
        var firstAudio = all.Where(l => l.FirstAudioMs is not null).Select(l => l.FirstAudioMs!.Value).Order().ToList();

        Console.WriteLine();
        Console.WriteLine($"Utterances {all.Count} · spoken {settled.Count} · skipped as stale {skipped} · cut {all.Count(l => l.CutAfterMs is not null)} · state {lastState?.Clock ?? "none"} {lastState?.HomeScore}–{lastState?.AwayScore}");
        var failures = new List<string>();
        if (all.Count == 0)
        {
            failures.Add("no utterances received");
        }

        // Every spoken line must have its audio, in order, with a matching end marker.
        var audioMs = settled.Sum(l => l.Bytes) / 48.0;
        Console.WriteLine($"Audio {settled.Sum(l => l.Bytes) / 1024} KiB ({audioMs / 1000:0.0} s) · receive→first audio p50 {Percentile(firstAudio, 0.5):0} ms, p95 {Percentile(firstAudio, 0.95):0} ms");
        Console.WriteLine($"First audio by line (ms): {string.Join(", ", all.Where(l => l.FirstAudioMs is not null).Select(l => $"{l.FirstAudioMs:0}"))}");
        Check(settled.All(l => l.Bytes > 0), "an utterance ended without audio");
        Check(settled.All(l => !l.OutOfOrder), "audio chunks arrived out of order");
        foreach (var l in settled.Where(l => l.SpokenMs != l.Bytes / 48))
        {
            Console.WriteLine($"  mismatch: '{l.Utterance.Text}' audioEnd {l.SpokenMs?.ToString() ?? "missing"} ms, bytes {l.Bytes} ({l.Bytes / 48} ms), captions {l.Cues}, cut {l.CutAfterMs?.ToString() ?? "no"}");
        }

        Check(settled.All(l => l.SpokenMs == l.Bytes / 48), "audioEnd duration doesn't match the bytes sent");
        Check(settled.All(l => l.Cues > 0), "an utterance had no captions");
        Check(settled.Count > 0, "no utterance finished");

        // The pitch stream: about five a second, with the ball and both teams.
        if (pitches.Count > 1)
        {
            var span = Stopwatch.GetElapsedTime(pitches[0].ReceivedAt, pitches[^1].ReceivedAt).TotalSeconds;
            var rate = (pitches.Count - 1) / span;
            var last = pitches[^1].Pitch;
            Console.WriteLine($"Pitch {pitches.Count} updates · {rate:0.0} a second · ball ({last.B[0]}, {last.B[1]}) · {last.H.Length} v {last.A.Length} players · possession {last.O ?? "none"}");
            Check(rate is > 4 and < 6, $"pitch updates came {rate:0.0} a second, not about 5");
            Check(last.H.Length >= 7 && last.A.Length >= 7, "a pitch update is missing players");
        }
        else
        {
            Check(false, "no pitch updates arrived");
        }

        using var http = new HttpClient();
        var stats = await http.GetFromJsonAsync<JsonElement>($"{url}/stats");
        Console.WriteLine($"Gateway /stats: {stats}");

        foreach (var failure in failures)
        {
            Console.WriteLine($"FAIL: {failure}");
        }

        Console.WriteLine(failures.Count == 0 ? "PASS" : "FAILED");
        return failures.Count == 0 ? 0 : 1;

        void Check(bool ok, string message)
        {
            if (!ok)
            {
                failures.Add(message);
            }
        }
    }

    private static double Percentile(List<double> sorted, double p) =>
        sorted.Count == 0 ? double.NaN : sorted[(int)Math.Min(sorted.Count - 1, Math.Floor(p * sorted.Count))];
}
