namespace Vog.Contracts;

public enum Role
{
    Goalkeeper,
    CentreBack,
    FullBack,
    DefensiveMidfielder,
    CentralMidfielder,
    Winger,
    Striker,
}

public sealed record PlayerInfo(string PlayerId, string Name, string ShortName, int Number, Role Role);

/// <summary>
/// A team as the audience meets it. <see cref="Nickname"/> is the kit-based on-air name
/// ("Reds"), which audio description uses so listeners can track teams by colour.
/// </summary>
public sealed record TeamInfo(
    string TeamId,
    string Name,
    string Nickname,
    string KitColour,
    IReadOnlyList<PlayerInfo> Starters,
    IReadOnlyList<PlayerInfo> Bench);

/// <summary>Match metadata, published once before kick-off.</summary>
public sealed record MatchInfo(string MatchId, string Competition, TeamInfo Home, TeamInfo Away, ulong Seed)
{
    public TeamInfo Team(string teamId) => teamId == Home.TeamId ? Home : Away;

    public TeamInfo Opponent(string teamId) => teamId == Home.TeamId ? Away : Home;

    /// <summary>The home team attacks towards increasing X in the first period.</summary>
    public bool AttacksRight(string teamId, int period) => (teamId == Home.TeamId) == (period == 1);

    public PlayerInfo? FindPlayer(string? playerId) =>
        playerId is null
            ? null
            : Home.Starters.Concat(Home.Bench).Concat(Away.Starters).Concat(Away.Bench)
                .FirstOrDefault(p => p.PlayerId == playerId);
}

/// <summary>One 100 ms step of the match feed: a tracking frame plus any events and scenes in it.</summary>
public sealed record MatchTick(
    TrackingFrame Frame,
    IReadOnlyList<MatchEvent> Events,
    IReadOnlyList<SceneEvent> Scenes);
