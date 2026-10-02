namespace Vog.Voice;

/// <summary>Writes 16-bit mono PCM as a WAV file.</summary>
public static class Wav
{
    public static void Write(string path, ReadOnlySpan<byte> pcm, int sampleRate = NeuralSynthesizer.SampleRate)
    {
        using var stream = File.Create(path);
        using var w = new BinaryWriter(stream);
        const short channels = 1;
        const short bitsPerSample = 16;
        var byteRate = sampleRate * channels * bitsPerSample / 8;

        w.Write("RIFF"u8);
        w.Write(36 + pcm.Length);
        w.Write("WAVE"u8);
        w.Write("fmt "u8);
        w.Write(16);
        w.Write((short)1); // PCM
        w.Write(channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write((short)(channels * bitsPerSample / 8));
        w.Write(bitsPerSample);
        w.Write("data"u8);
        w.Write(pcm.Length);
        w.Write(pcm);
    }

    /// <summary>Bytes of PCM for a duration at 24 kHz 16-bit mono (always an even number).</summary>
    public static int BytesFor(long milliseconds, int sampleRate = NeuralSynthesizer.SampleRate) =>
        (int)(milliseconds * sampleRate / 1000) * 2;

    /// <summary>Duration of 16-bit mono PCM, in milliseconds.</summary>
    public static long MillisecondsFor(int bytes, int sampleRate = NeuralSynthesizer.SampleRate) =>
        bytes / 2 * 1000L / sampleRate;
}
