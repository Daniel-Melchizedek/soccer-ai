using Vog.Domain;
using Vog.Simulator;

namespace Vog.Pipeline;

/// <summary>The fixed fictional league and fixture used for demos and golden-match tests.</summary>
public static class GoldenMatch
{
    public const ulong LeagueSeed = 2026;
    public const ulong DefaultMatchSeed = 20261026;

    private static readonly Lazy<IReadOnlyList<Club>> League = new(() => LeagueFactory.CreateLeague(LeagueSeed));

    public static IReadOnlyList<Club> Clubs => League.Value;

    /// <summary>Club 0 wears red and club 1 blue, so the demo match is "Reds v Blues".</summary>
    public static MatchSetup Setup(ulong matchSeed = DefaultMatchSeed) =>
        new($"m-{matchSeed}", League.Value[0], League.Value[1], matchSeed);
}
