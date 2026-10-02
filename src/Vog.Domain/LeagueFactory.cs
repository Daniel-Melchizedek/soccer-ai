using Vog.Contracts;

namespace Vog.Domain;

/// <summary>
/// Generates a fictional league: club names from syllables (checked against a deny-list of real
/// clubs), kit-coloured nicknames, and squads with role-appropriate attributes.
/// No real club, player or trademark is used anywhere.
/// </summary>
public static class LeagueFactory
{
    private static readonly (string Kit, string Nickname)[] Kits =
    [
        ("red", "Reds"), ("blue", "Blues"), ("white", "Whites"), ("yellow", "Yellows"), ("green", "Greens"),
        ("claret", "Clarets"), ("sky_blue", "Sky Blues"), ("orange", "Ambers"), ("black", "Blacks"), ("purple", "Violets"),
    ];

    private static readonly string[] TownStarts =
        ["Ash", "Brack", "Cal", "Dun", "Elm", "Fen", "Glen", "Hart", "Kes", "Lark", "Marl", "Nor", "Oak", "Pen", "Rook", "Sel", "Thorn", "Wey", "Yar", "Quill"];

    private static readonly string[] TownEnds =
        ["ford", "by", "wick", "mouth", "ton", "bridge", "field", "stead", "haven", "combe", "dale", "holm", "port", "minster", "worth"];

    private static readonly string[] ClubSuffixes =
        ["Athletic", "Town", "United", "City", "Rovers", "Albion", "Borough", "Rangers"];

    /// <summary>Lower-case tokens of real professional clubs; generated names must not contain any.</summary>
    public static readonly IReadOnlySet<string> DeniedTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "arsenal", "aston", "villa", "bournemouth", "brentford", "brighton", "burnley", "chelsea", "crystal", "palace",
        "everton", "fulham", "ipswich", "leeds", "leicester", "liverpool", "luton", "manchester", "newcastle", "nottingham",
        "forest", "sheffield", "southampton", "sunderland", "tottenham", "hotspur", "westham", "wolverhampton", "wolves",
        "watford", "norwich", "stoke", "middlesbrough", "derby", "blackburn", "bolton", "wigan", "reading", "swansea",
        "cardiff", "hull", "coventry", "birmingham", "portsmouth", "plymouth", "preston", "millwall", "barnsley", "oxford",
        "cambridge", "ashford", "stanley", "wycombe", "exeter", "bristol", "charlton", "northampton", "peterborough",
    };

    private static readonly string[] FirstNames =
    [
        "Kwame", "Mateo", "Luca", "Tomas", "Idris", "Rafael", "Jonah", "Emil", "Sami", "Kenji", "Dario", "Felix",
        "Bruno", "Arjun", "Nico", "Oskar", "Yusuf", "Theo", "Malik", "Andrei", "Caio", "Elias", "Marek", "Tariq",
        "Hugo", "Ilan", "Joel", "Kofi", "Leon", "Milan", "Omar", "Pavel", "Rui", "Soren", "Tiago", "Viktor",
    ];

    private static readonly string[] SurnameStarts =
        ["Oka", "Men", "Dua", "Sil", "Kov", "Bar", "Tal", "Nwo", "Fer", "Lun", "Hal", "Ros", "Vel", "Ama", "Kir", "Dra", "Mor", "Sev", "Ola", "Pet", "Ren", "Zan"];

    private static readonly string[] SurnameEnds =
        ["for", "sah", "rte", "va", "ic", "ton", "ley", "ez", "ini", "berg", "sen", "ko", "ye", "dor", "ri", "ma", "son", "lo", "wa", "nez", "ard", "ovic"];

    private static readonly Role[] BenchRoles =
        [Role.Goalkeeper, Role.CentreBack, Role.FullBack, Role.DefensiveMidfielder, Role.CentralMidfielder, Role.Winger, Role.Striker];

    private static readonly int[] StarterNumbers = [1, 2, 5, 6, 3, 4, 8, 10, 7, 9, 11];

    public static IReadOnlyList<Club> CreateLeague(ulong seed, int clubCount = 20)
    {
        var rng = new Rng(seed);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var clubs = new List<Club>(clubCount);

        for (var i = 0; i < clubCount; i++)
        {
            var name = UniqueClubName(rng, usedNames);
            var (kit, nickname) = Kits[i % Kits.Length];
            clubs.Add(new Club($"c-{i:D2}", name, nickname, kit, CreateSquad(rng, i)));
        }

        return clubs;
    }

    public static bool IsDenied(string name)
    {
        var compact = name.Replace(" ", string.Empty);
        return DeniedTokens.Any(t => compact.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

    private static string UniqueClubName(Rng rng, HashSet<string> used)
    {
        while (true)
        {
            var town = rng.Pick(TownStarts) + rng.Pick(TownEnds);
            var name = $"{town} {rng.Pick(ClubSuffixes)}";
            if (!IsDenied(name) && used.Add(town))
            {
                return name;
            }
        }
    }

    private static IReadOnlyList<PlayerProfile> CreateSquad(Rng rng, int clubIndex)
    {
        var squad = new List<PlayerProfile>(18);
        var usedSurnames = new HashSet<string>();

        for (var slot = 0; slot < Formation.FourThreeThree.Count; slot++)
        {
            squad.Add(CreatePlayer(rng, clubIndex, squad.Count, StarterNumbers[slot], Formation.FourThreeThree[slot].Role, usedSurnames));
        }

        for (var b = 0; b < BenchRoles.Length; b++)
        {
            squad.Add(CreatePlayer(rng, clubIndex, squad.Count, 13 + b, BenchRoles[b], usedSurnames));
        }

        return squad;
    }

    private static PlayerProfile CreatePlayer(Rng rng, int clubIndex, int index, int number, Role role, HashSet<string> usedSurnames)
    {
        string surname;
        do
        {
            surname = rng.Pick(SurnameStarts) + rng.Pick(SurnameEnds);
        }
        while (!usedSurnames.Add(surname));

        var (speed, passing, finishing, tackling) = role switch
        {
            Role.Goalkeeper => (rng.Uniform(7.0, 7.8), rng.Uniform(0.45, 0.62), 0.05, 0.3),
            Role.CentreBack => (rng.Uniform(8.0, 8.9), rng.Uniform(0.62, 0.76), rng.Uniform(0.2, 0.4), rng.Uniform(0.72, 0.9)),
            Role.FullBack => (rng.Uniform(8.6, 9.5), rng.Uniform(0.64, 0.78), rng.Uniform(0.25, 0.4), rng.Uniform(0.6, 0.8)),
            Role.DefensiveMidfielder => (rng.Uniform(8.2, 9.0), rng.Uniform(0.74, 0.88), rng.Uniform(0.3, 0.45), rng.Uniform(0.7, 0.85)),
            Role.CentralMidfielder => (rng.Uniform(8.3, 9.1), rng.Uniform(0.76, 0.9), rng.Uniform(0.4, 0.6), rng.Uniform(0.5, 0.7)),
            Role.Winger => (rng.Uniform(8.9, 9.8), rng.Uniform(0.66, 0.8), rng.Uniform(0.55, 0.75), rng.Uniform(0.3, 0.5)),
            _ => (rng.Uniform(8.7, 9.6), rng.Uniform(0.62, 0.76), rng.Uniform(0.65, 0.9), rng.Uniform(0.25, 0.45)),
        };

        return new PlayerProfile(
            $"p-{clubIndex:D2}-{index:D2}",
            rng.Pick(FirstNames),
            surname,
            number,
            role,
            speed,
            passing,
            finishing,
            tackling,
            rng.Uniform(0.6, 0.95));
    }
}
