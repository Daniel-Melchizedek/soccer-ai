namespace Vog.Contracts;

public enum SceneKind
{
    Kit,
    Weather,
    Celebration,
    BodyLanguage,
    RefereeSignal,
    Var,
    Crowd,
    SubBoard,
}

/// <summary>
/// A visual detail that an audio describer would mention but that isn't an on-the-ball event:
/// kits, celebrations, body language, referee signals, crowd. The scene describer may only describe
/// what is present in <see cref="Attrs"/> ("scene lock"), which keeps audio description grounded.
/// </summary>
public sealed record SceneEvent(
    string EventId,
    long MatchClockMs,
    SceneKind Kind,
    IReadOnlyList<string> Actors,
    IReadOnlyDictionary<string, string> Attrs,
    string? CauseEventId = null);
