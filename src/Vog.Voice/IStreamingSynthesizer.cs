namespace Vog.Voice;

public interface IStreamingSynthesizer : IDisposable
{
    event Action<byte[]>? AudioChunk;
    Task<SynthesisResult> SpeakAsync(string text, CancellationToken cancellationToken = default);
}