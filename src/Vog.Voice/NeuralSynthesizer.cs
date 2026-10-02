using System.Diagnostics;
using Azure.Core;
using Microsoft.CognitiveServices.Speech;

namespace Vog.Voice;

/// <summary>A spoken word and where it falls in the audio, from the service's word-boundary events.</summary>
public sealed record WordTiming(string Text, TimeSpan Offset, TimeSpan Duration, bool IsPunctuation);

public sealed record SynthesisResult(
    byte[] Pcm,
    TimeSpan AudioDuration,
    IReadOnlyList<WordTiming> Words,
    TimeSpan FirstByteLatency,
    TimeSpan TotalLatency);

/// <summary>
/// Text to speech for one channel: one pre-connected synthesizer, 24 kHz 16-bit mono PCM out,
/// audio streamed through <see cref="AudioChunk"/> as it arrives, word timings collected for captions.
/// </summary>
public sealed class NeuralSynthesizer : IDisposable
{
    public const int SampleRate = 24_000;

    private readonly SpeechSynthesizer _synthesizer;
    private readonly Connection _connection;
    private readonly VoiceProfile _voice;

    public NeuralSynthesizer(Uri endpoint, TokenCredential credential, VoiceProfile voice)
    {
        _voice = voice;
        var config = SpeechConfig.FromEndpoint(endpoint, credential);
        config.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);

        // No AudioConfig: audio comes back to us rather than playing on this machine's speakers.
        _synthesizer = new SpeechSynthesizer(config, null);
        _connection = Connection.FromSpeechSynthesizer(_synthesizer);
        _connection.Open(true);
    }

    /// <summary>Raised for each audio chunk as it streams in (for low-latency playout).</summary>
    public event Action<byte[]>? AudioChunk;

    public VoiceProfile Voice => _voice;

    public async Task<SynthesisResult> SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        var words = new List<WordTiming>();
        var clock = Stopwatch.StartNew();
        TimeSpan? firstByte = null;

        void OnSynthesizing(object? sender, SpeechSynthesisEventArgs e)
        {
            firstByte ??= clock.Elapsed;
            AudioChunk?.Invoke(e.Result.AudioData);
        }

        void OnWordBoundary(object? sender, SpeechSynthesisWordBoundaryEventArgs e) =>
            words.Add(new WordTiming(e.Text, TimeSpan.FromTicks((long)e.AudioOffset), e.Duration, e.BoundaryType == SpeechSynthesisBoundaryType.Punctuation));

        _synthesizer.Synthesizing += OnSynthesizing;
        _synthesizer.WordBoundary += OnWordBoundary;
        try
        {
            using var registration = cancellationToken.Register(() => _synthesizer.StopSpeakingAsync());
            using var result = await _synthesizer.SpeakSsmlAsync(Ssml.For(_voice, text));
            if (result.Reason != ResultReason.SynthesizingAudioCompleted)
            {
                var details = SpeechSynthesisCancellationDetails.FromResult(result);
                throw new InvalidOperationException($"Speech synthesis failed ({details.Reason}, {details.ErrorCode}): {details.ErrorDetails}");
            }

            return new SynthesisResult(result.AudioData, result.AudioDuration, words, firstByte ?? clock.Elapsed, clock.Elapsed);
        }
        finally
        {
            _synthesizer.Synthesizing -= OnSynthesizing;
            _synthesizer.WordBoundary -= OnWordBoundary;
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
        _synthesizer.Dispose();
    }
}
