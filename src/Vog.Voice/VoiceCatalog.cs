using Vog.Contracts;

namespace Vog.Voice;

/// <summary>A voice and speaking rate for one channel.</summary>
public sealed record VoiceProfile(string Lang, string VoiceName, string ProsodyRate);

/// <summary>
/// Live audio uses standard neural voices: they support the full SSML the live path needs
/// (prosody rate, breaks, phonemes, word boundaries). HD and Omni voices don't support
/// prosody or break, so they are reserved for recaps (BUILD_PLAN.md D7).
/// </summary>
public static class VoiceCatalog
{
    private static readonly IReadOnlyDictionary<string, string> Describers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en-GB"] = "en-GB-RyanNeural",
        ["es-ES"] = "es-ES-AlvaroNeural",
        ["ta-IN"] = "ta-IN-ValluvarNeural",
    };

    /// <summary>The voice Q&amp;A companion answers in a different voice, so a listener always knows who is speaking.</summary>
    private static readonly IReadOnlyDictionary<string, string> Companions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en-GB"] = "en-GB-SoniaNeural",
        ["es-ES"] = "es-ES-ElviraNeural",
        ["ta-IN"] = "ta-IN-PallaviNeural",
    };

    public static string Companion(string lang) =>
        Companions.TryGetValue(lang, out var voice) ? voice : throw new KeyNotFoundException($"No companion voice configured for {lang}.");

    public static VoiceProfile For(string lang, ListenerMode mode)
    {
        if (!Describers.TryGetValue(lang, out var voice))
        {
            throw new KeyNotFoundException($"No live voice configured for {lang}.");
        }

        // Audio description is brisk, as describers are; Calm is deliberately unhurried.
        var rate = mode switch
        {
            ListenerMode.AudioDescription => "+30%",
            ListenerMode.Calm => "-5%",
            ListenerMode.Kids => "+0%",
            _ => "+8%",
        };

        return new VoiceProfile(lang, voice, rate);
    }
}
