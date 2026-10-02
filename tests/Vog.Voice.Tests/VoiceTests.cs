using Vog.Contracts;

namespace Vog.Voice.Tests;

public sealed class CaptionTests
{
    private static List<WordTiming> Words(string sentence, int msPerWord = 300)
    {
        var words = new List<WordTiming>();
        var t = 0;
        foreach (var token in sentence.Split(' '))
        {
            var text = token.TrimEnd(',', '.', '!');
            words.Add(new WordTiming(text, TimeSpan.FromMilliseconds(t), TimeSpan.FromMilliseconds(msPerWord - 50), false));
            t += msPerWord;
            if (text.Length < token.Length)
            {
                words.Add(new WordTiming(token[text.Length..], TimeSpan.FromMilliseconds(t - 50), TimeSpan.Zero, true));
            }
        }

        return words;
    }

    [Fact]
    public void CuesKeepToTwoLinesOfThirtySevenCharacters()
    {
        var text = "Intercepted by Talye, Blues ball, on the right in the final third. Long ball forward, Rosovic picks it up deep on the left.";
        var cues = Captions.FromWords(10_000, Words(text), text, 5000);

        Assert.True(cues.Count >= 2);
        Assert.All(cues, c =>
        {
            Assert.InRange(c.Lines.Count, 1, Captions.MaxLines);
            Assert.All(c.Lines, line => Assert.True(line.Length <= Captions.MaxLineLength, $"'{line}' is {line.Length} characters"));
        });
        Assert.Equal(text, string.Join(' ', cues.SelectMany(c => c.Lines)));
    }

    [Fact]
    public void CuesAreTimedToTheWordsOnThePresentationTimeline()
    {
        var cues = Captions.FromWords(10_000, Words("Saved by Petlo!"), "Saved by Petlo!", 1000);

        var cue = Assert.Single(cues);
        Assert.Equal(10_000, cue.StartMs);
        Assert.Equal("Saved by Petlo!", cue.Lines.Single());
        Assert.True(cue.EndMs > cue.StartMs);
    }

    [Fact]
    public void WebVttUsesTheStandardHeaderAndTimestamps()
    {
        var vtt = Captions.ToWebVtt([new CaptionCue(61_500, 63_250, ["GOAL! Zandor!"])]);
        Assert.StartsWith("WEBVTT\n\n", vtt, StringComparison.Ordinal);
        Assert.Contains("00:01:01.500 --> 00:01:03.250\nGOAL! Zandor!\n", vtt, StringComparison.Ordinal);
    }
}

public sealed class PcmTests
{
    [Fact]
    public void PreemptionCutsAtTheLastWholeWord()
    {
        var pcm = Tone(milliseconds: 2000);
        var words = new List<WordTiming>
        {
            new("Okafor", TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(400), false),
            new("to", TimeSpan.FromMilliseconds(450), TimeSpan.FromMilliseconds(150), false),
            new("Mensah", TimeSpan.FromMilliseconds(650), TimeSpan.FromMilliseconds(500), false),
        };

        var cut = PcmEdit.CutAtWordBoundary(pcm, words, maxMs: 900);

        Assert.Equal(600, Wav.MillisecondsFor(cut.Length)); // after "to", not mid-"Mensah"
        Assert.Equal(0, BitConverter.ToInt16(cut, cut.Length - 2)); // faded to silence
    }

    [Fact]
    public void WavHeaderDescribes24KhzMono16Bit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"vog-{Guid.NewGuid():N}.wav");
        try
        {
            var pcm = Tone(milliseconds: 100);
            Wav.Write(path, pcm);
            var bytes = File.ReadAllBytes(path);

            Assert.Equal(44 + pcm.Length, bytes.Length);
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal(24_000, BitConverter.ToInt32(bytes, 24));
            Assert.Equal(1, BitConverter.ToInt16(bytes, 22));
            Assert.Equal(16, BitConverter.ToInt16(bytes, 34));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ByteAndDurationConversionsRoundTrip() =>
        Assert.Equal(1234, Wav.MillisecondsFor(Wav.BytesFor(1234)));

    private static byte[] Tone(int milliseconds)
    {
        var pcm = new byte[Wav.BytesFor(milliseconds)];
        for (var i = 0; i < pcm.Length / 2; i++)
        {
            var sample = (short)(8000 * Math.Sin(i * 2 * Math.PI * 440 / NeuralSynthesizer.SampleRate));
            pcm[2 * i] = (byte)(sample & 0xFF);
            pcm[2 * i + 1] = (byte)((sample >> 8) & 0xFF);
        }

        return pcm;
    }
}

public sealed class SsmlTests
{
    [Fact]
    public void TextIsEscapedAndTheChannelsVoiceAndRateApply()
    {
        var voice = VoiceCatalog.For("en-GB", ListenerMode.AudioDescription);
        var ssml = Ssml.For(voice, "Reds & Blues <draw>");

        Assert.Contains("Reds &amp; Blues &lt;draw&gt;", ssml, StringComparison.Ordinal);
        Assert.Contains($"name=\"{voice.VoiceName}\"", ssml, StringComparison.Ordinal);
        Assert.Contains($"rate=\"{voice.ProsodyRate}\"", ssml, StringComparison.Ordinal);
        Assert.Contains("xml:lang=\"en-GB\"", ssml, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryLanguageAndModeHasAVoice()
    {
        foreach (var lang in new[] { "en-GB", "es-ES" })
        {
            foreach (var mode in Enum.GetValues<ListenerMode>())
            {
                Assert.False(string.IsNullOrEmpty(VoiceCatalog.For(lang, mode).VoiceName));
            }
        }
    }
}
