using System.Globalization;
using System.Text;

namespace Vog.Voice;

/// <summary>A caption on the presentation timeline.</summary>
public sealed record CaptionCue(long StartMs, long EndMs, IReadOnlyList<string> Lines);

/// <summary>
/// Builds live captions from word timings, following BBC live-subtitle practice: at most two
/// lines of 37 characters, timed to the words as they are spoken.
/// </summary>
public static class Captions
{
    public const int MaxLineLength = 37;
    public const int MaxLines = 2;

    public static IReadOnlyList<CaptionCue> FromWords(long utteranceStartMs, IReadOnlyList<WordTiming> words, string fallbackText, long fallbackDurationMs)
    {
        // With no word timings (rare), caption the whole utterance for its duration.
        if (words.Count == 0)
        {
            return Pack([(fallbackText, 0L, fallbackDurationMs)], utteranceStartMs);
        }

        var tokens = new List<(string Text, long StartMs, long EndMs)>();
        foreach (var w in words)
        {
            var start = (long)w.Offset.TotalMilliseconds;
            var end = start + (long)w.Duration.TotalMilliseconds;

            // Punctuation attaches to the word before it.
            if (w.IsPunctuation && tokens.Count > 0)
            {
                var last = tokens[^1];
                tokens[^1] = (last.Text + w.Text, last.StartMs, Math.Max(last.EndMs, end));
                continue;
            }

            tokens.Add((w.Text, start, end));
        }

        return Pack(tokens, utteranceStartMs);
    }

    public static string ToWebVtt(IEnumerable<CaptionCue> cues)
    {
        var sb = new StringBuilder("WEBVTT\n\n");
        foreach (var cue in cues)
        {
            sb.Append(Timestamp(cue.StartMs)).Append(" --> ").Append(Timestamp(cue.EndMs)).Append('\n');
            foreach (var line in cue.Lines)
            {
                sb.Append(line).Append('\n');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static List<CaptionCue> Pack(IReadOnlyList<(string Text, long StartMs, long EndMs)> tokens, long offsetMs)
    {
        var cues = new List<CaptionCue>();
        var lines = new List<string>();
        var line = new StringBuilder();
        long cueStart = -1;
        long cueEnd = 0;

        void FlushCue()
        {
            if (line.Length > 0)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (lines.Count > 0)
            {
                cues.Add(new CaptionCue(offsetMs + cueStart, offsetMs + cueEnd, lines.ToList()));
                lines.Clear();
            }

            cueStart = -1;
        }

        foreach (var (text, start, end) in tokens)
        {
            if (line.Length > 0 && line.Length + 1 + text.Length > MaxLineLength)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == MaxLines)
                {
                    FlushCue();
                }
            }

            if (cueStart < 0)
            {
                cueStart = start;
            }

            line.Append(line.Length == 0 ? string.Empty : " ").Append(text);
            cueEnd = end;
        }

        FlushCue();
        return cues;
    }

    private static string Timestamp(long ms)
    {
        var t = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}.{3:000}", (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds);
    }
}
