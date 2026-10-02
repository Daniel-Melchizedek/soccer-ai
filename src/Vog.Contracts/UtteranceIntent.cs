using System.Text.Json.Serialization;

namespace Vog.Contracts;

public enum IntentKind
{
    PbpPass,
    PbpCarry,
    BallLocation,
    PossessionChange,
    Shot,
    ShotOutcome,
    Goal,
    Card,
    Substitution,
    Foul,
    SetPiece,
    Offside,
    ScoreTime,
    DirectionOfPlay,
    Scene,
    PeriodChange,
}

/// <summary>P0 interrupts anything; P3/P4 only air during dead balls.</summary>
public enum Priority
{
    P0 = 0,
    P1 = 1,
    P2 = 2,
    P3 = 3,
    P4 = 4,
}

/// <summary>
/// A typed, language-neutral value inside an utterance. Keeping slots typed (rather than
/// pre-formatted strings) lets each language realizer handle its own grammar and units.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$slot")]
[JsonDerivedType(typeof(PlayerSlot), "player")]
[JsonDerivedType(typeof(TeamSlot), "team")]
[JsonDerivedType(typeof(ZoneSlot), "zone")]
[JsonDerivedType(typeof(NumberSlot), "number")]
[JsonDerivedType(typeof(ClockSlot), "clock")]
[JsonDerivedType(typeof(ScoreSlot), "score")]
[JsonDerivedType(typeof(SideSlot), "side")]
public abstract record Slot;

public sealed record PlayerSlot(string PlayerId, string Name) : Slot;

/// <summary>A team, referred to on air by its kit-based nickname (for example "Reds").</summary>
public sealed record TeamSlot(string TeamId, string Nickname, string KitColour) : Slot;

public sealed record ZoneSlot(Zone Zone) : Slot;

/// <summary>A measured number; <see cref="Unit"/> is one of "kmh", "m" or "count".</summary>
public sealed record NumberSlot(double Value, string Unit) : Slot;

public sealed record ClockSlot(int Minute, int AddedMinute) : Slot;

public sealed record ScoreSlot(string HomeNickname, int Home, string AwayNickname, int Away) : Slot;

/// <summary>A side of the pitch from the listener's seat in the main stand.</summary>
public sealed record SideSlot(bool Left) : Slot;

public enum IntentSource
{
    Template,
    Producer,
    Agent,
}

/// <summary>
/// What should be said, produced once per moment and rendered per channel (language × mode).
/// The Booth Producer schedules intents; it never invents facts.
/// </summary>
public sealed record UtteranceIntent(
    string IntentId,
    string MatchId,
    IntentKind Kind,
    string TemplateKey,
    IReadOnlyDictionary<string, Slot> Slots,
    Priority Priority,
    double Value,
    long PtsMs,
    long ValidUntilMs,
    bool MentionsBallLocation = false,
    string? SupersedeKey = null,
    bool Interruptible = true,
    double Energy = 0.5,
    string? EarconId = null,
    PitchPoint? Spatial = null,
    IntentSource Source = IntentSource.Template,
    string? CauseEventId = null);
