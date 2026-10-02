namespace Vog.Voice;

/// <summary>Edits on 16-bit mono PCM used by playout.</summary>
public static class PcmEdit
{
    public const int FadeMs = 30;

    /// <summary>
    /// Cuts speech for preemption: at the end of the last word that finishes by
    /// <paramref name="maxMs"/> (never mid-word), with a short fade so the cut doesn't click.
    /// </summary>
    public static byte[] CutAtWordBoundary(byte[] pcm, IReadOnlyList<WordTiming> words, long maxMs)
    {
        var cutMs = 0L;
        foreach (var w in words)
        {
            var end = (long)(w.Offset + w.Duration).TotalMilliseconds;
            if (end <= maxMs)
            {
                cutMs = Math.Max(cutMs, end);
            }
        }

        // No word fits: cut at the limit itself rather than drop the audio entirely.
        if (cutMs == 0)
        {
            cutMs = maxMs;
        }

        var length = Math.Min(pcm.Length, Wav.BytesFor(cutMs));
        var cut = pcm.AsSpan(0, length).ToArray();
        FadeOut(cut, FadeMs);
        return cut;
    }

    public static void FadeOut(byte[] pcm, int fadeMs)
    {
        var samples = pcm.Length / 2;
        var fadeSamples = Math.Min(samples, Wav.BytesFor(fadeMs) / 2);
        for (var i = 0; i < fadeSamples; i++)
        {
            var index = (samples - fadeSamples + i) * 2;
            var sample = (short)(pcm[index] | (pcm[index + 1] << 8));
            var scaled = (short)(sample * (1.0 - (double)(i + 1) / fadeSamples));
            pcm[index] = (byte)(scaled & 0xFF);
            pcm[index + 1] = (byte)((scaled >> 8) & 0xFF);
        }
    }
}
