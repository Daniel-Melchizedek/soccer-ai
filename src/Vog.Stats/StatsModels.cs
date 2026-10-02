using Vog.Contracts;
using Vog.Domain.Models;

namespace Vog.Stats;

/// <summary>Metrics derived for one event (stage b: interpret).</summary>
public sealed record Enrichment(
    double? PassDistanceM = null,
    double? BallSpeedKmh = null,
    double? PSuccess = null,
    double? Difficulty = null,
    IReadOnlyList<PassModel.Contribution>? DifficultyFactors = null,
    double? Xg = null,
    double? ShotSpeedKmh = null,
    double? ShotDistanceM = null)
{
    public static Enrichment None { get; } = new();
}

public sealed record EnrichedEvent(MatchEvent Event, Enrichment Enrichment);

public enum PlayerTagKind
{
    /// <summary>A sprint (≥ 25.2 km/h held ≥ 1 s) ended; value is its peak speed in km/h.</summary>
    Sprint,

    /// <summary>A new fastest speed of the match; value in km/h.</summary>
    TopSpeed,

    /// <summary>Distance covered passed a milestone; value in km.</summary>
    DistanceMilestone,
}

/// <summary>A threshold-triggered player tag, as shown on screen and read aloud.</summary>
public sealed record PlayerTag(PlayerTagKind Kind, string PlayerId, string TeamId, long MatchClockMs, double Value);

public enum BallStatus
{
    InPlay,
    Dead,
}

/// <summary>Where the ball is and who has it, from the point of view of the team in possession.</summary>
public sealed record BallState(
    int Period,
    long MatchClockMs,
    BallStatus Status,
    bool InFlight,
    string? PossessionTeamId,
    string? CarrierId,
    PitchPoint Position,
    Zone? Zone,
    MatchEventType? PendingRestart,
    long LastRestartMs,
    int HomeScore,
    int AwayScore);

/// <summary>Everything the interpret stage produced for one tick.</summary>
public sealed record StatsUpdate(
    TrackingFrame Frame,
    BallState Ball,
    IReadOnlyList<EnrichedEvent> Events,
    IReadOnlyList<PlayerTag> Tags,
    IReadOnlyList<SceneEvent> Scenes);
