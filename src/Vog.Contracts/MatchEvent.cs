namespace Vog.Contracts;

public enum MatchEventType
{
    PeriodStart,
    PeriodEnd,
    KickOff,
    Pass,
    Carry,
    Clearance,
    Shot,
    Save,
    Goal,
    Tackle,
    Foul,
    Card,
    Offside,
    BallOut,
    ThrowIn,
    Corner,
    GoalKick,
    FreeKick,
    Substitution,
    PossessionChange,
}

public enum EventOutcome
{
    None,
    Complete,
    Incomplete,
    Won,
    OnTarget,
    OffTarget,
    Blocked,
    Saved,
    Goal,
    Yellow,
    SecondYellow,
    Red,
}

public enum PassHeight
{
    Ground,
    Lofted,
}

public enum PlayPhase
{
    BuildUp,
    Progression,
    FinalThird,
    Transition,
    SetPiece,
    Dead,
}

/// <summary>
/// One on-the-ball or match-control event, as emitted by the simulator (stage a: ingest).
/// Raw facts only: derived metrics such as pass difficulty or shot speed are added later by the
/// stats engine (stage b: interpret), so the pipeline never trusts the generator's internals.
/// </summary>
/// <param name="ReceiverId">Intended receiver of a pass.</param>
/// <param name="OpponentId">The opposing player involved: tackled, fouled or intercepting.</param>
/// <param name="ReplacedPlayerId">For substitutions, the player leaving the pitch.</param>
public sealed record MatchEvent(
    string EventId,
    string MatchId,
    long Seq,
    int Period,
    long MatchClockMs,
    string DisplayClock,
    MatchEventType Type,
    string? TeamId,
    string? PlayerId,
    PitchPoint? Location,
    PitchPoint? EndLocation = null,
    EventOutcome Outcome = EventOutcome.None,
    string? ReceiverId = null,
    string? OpponentId = null,
    string? ReplacedPlayerId = null,
    PassHeight? Height = null,
    bool UnderPressure = false,
    int PossessionId = 0,
    string? PossessionTeamId = null,
    PlayPhase Phase = PlayPhase.Progression,
    string? RelatedEventId = null);
