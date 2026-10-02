using System.Security;

namespace Vog.Voice;

public static class Ssml
{
    /// <summary>Wraps plain text in SSML for a neural voice. Text is XML-escaped.</summary>
    public static string For(VoiceProfile voice, string text) =>
        $"""<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="{voice.Lang}"><voice name="{voice.VoiceName}"><prosody rate="{voice.ProsodyRate}">{SecurityElement.Escape(text)}</prosody></voice></speak>""";
}
