using System.Collections.Immutable;
using Vog.Contracts;
using Vog.Domain;
using Vog.Stats;

namespace Vog.Pipeline;

/// <summary>
/// An immutable view of the match as of the latest tick, replaced every tick, so readers on
/// other threads (the voice companion's tools) never see a half-updated state. Every fact a
/// tool gives a fan comes from here: nothing is left for a model to guess.
/// </summary>
public sealed record MatchSnapshot(
    MatchInfo Info,
    TrackingFrame Frame,
    BallState Ball,
    TeamTotals Home,
    TeamTotals Away,
    ImmutableList<MatchEvent> KeyEvents)
{
    public string Clock => MatchClock.Display(Frame.Period, Frame.MatchClockMs);

    public bool FullTime => KeyEvents.Any(e => e.Type == MatchEventType.PeriodEnd && e.Period == 2);

    public TeamTotals Totals(string teamId) => teamId == Info.Home.TeamId ? Home : Away;

    /// <summary>The events a fan asks about; passes, carries and restarts are left out.</summary>
    public static bool IsKey(MatchEventType type) => type is
        MatchEventType.PeriodStart or MatchEventType.PeriodEnd or MatchEventType.Shot or MatchEventType.Goal or
        MatchEventType.Card or MatchEventType.Substitution or MatchEventType.Foul or MatchEventType.Offside or
        MatchEventType.Corner;
}

public sealed record TeamTotals(
    int Shots,
    int ShotsOnTarget,
    double Xg,
    int PassesAttempted,
    int PassesCompleted,
    int Corners,
    int Fouls,
    int YellowCards,
    int RedCards,
    int Offsides,
    double PossessionShare)
{
    public static TeamTotals From(TeamStats s, double possessionShare) =>
        new(s.Shots, s.ShotsOnTarget, s.Xg, s.PassesAttempted, s.PassesCompleted, s.Corners, s.Fouls, s.YellowCards, s.RedCards, s.Offsides, possessionShare);
}
