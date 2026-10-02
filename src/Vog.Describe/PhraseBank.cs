using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vog.Describe;

public enum PhraseStatus
{
    /// <summary>Not yet reviewed by a native-speaking describer; never use on air.</summary>
    Draft,

    Reviewed,
}

/// <summary>
/// The words for one language and persona. Templates are chosen per intent; tables map typed
/// slots (zones, kits, units, clock, score) to natural phrases in that language.
/// </summary>
public sealed class PhraseBank
{
    public required string Lang { get; init; }

    public required string Persona { get; init; }

    public required PhraseStatus Status { get; init; }

    public required string DecimalSeparator { get; init; }

    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Phrases { get; init; }

    public required IReadOnlyDictionary<string, string> Zones { get; init; }

    public required IReadOnlyDictionary<string, string> Spots { get; init; }

    public required IReadOnlyDictionary<string, string> Sides { get; init; }

    public required IReadOnlyDictionary<string, string> Kits { get; init; }

    /// <summary>Optional on-air team names by kit colour (for example "los Rojos").</summary>
    public required IReadOnlyDictionary<string, string> Teams { get; init; }

    public required IReadOnlyDictionary<string, string> Units { get; init; }

    public required IReadOnlyDictionary<string, string> Clock { get; init; }

    public required IReadOnlyDictionary<string, string> Score { get; init; }
}

public static class PhraseBankLoader
{
    private const string ResourcePrefix = "PhraseBanks/";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    private static readonly Lazy<IReadOnlyList<PhraseBank>> Embedded = new(LoadAllEmbedded);

    public static IReadOnlyList<PhraseBank> All => Embedded.Value;

    public static PhraseBank Get(string lang, string persona = "describer") =>
        All.FirstOrDefault(b => b.Lang.Equals(lang, StringComparison.OrdinalIgnoreCase) && b.Persona == persona)
        ?? throw new KeyNotFoundException($"No phrase bank for {lang}/{persona}. Available: {string.Join(", ", All.Select(b => $"{b.Lang}/{b.Persona}"))}");

    public static PhraseBank Parse(string yaml)
    {
        var dto = Deserializer.Deserialize<PhraseBankDto>(yaml)
            ?? throw new InvalidDataException("Empty phrase bank.");

        return new PhraseBank
        {
            Lang = dto.Lang ?? throw new InvalidDataException("Phrase bank is missing 'lang'."),
            Persona = dto.Persona ?? "describer",
            Status = Enum.Parse<PhraseStatus>(dto.Status ?? "draft", ignoreCase: true),
            DecimalSeparator = dto.DecimalSeparator ?? ".",
            Phrases = (dto.Phrases ?? []).ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            Zones = dto.Zones ?? [],
            Spots = dto.Spots ?? [],
            Sides = dto.Sides ?? [],
            Kits = dto.Kits ?? [],
            Teams = dto.Teams ?? [],
            Units = dto.Units ?? [],
            Clock = dto.Clock ?? [],
            Score = dto.Score ?? [],
        };
    }

    private static IReadOnlyList<PhraseBank> LoadAllEmbedded()
    {
        var assembly = typeof(PhraseBankLoader).Assembly;
        var banks = new List<PhraseBank>();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            banks.Add(Parse(reader.ReadToEnd()));
        }

        return banks;
    }

    private sealed class PhraseBankDto
    {
        public string? Lang { get; set; }

        public string? Persona { get; set; }

        public string? Status { get; set; }

        public string? DecimalSeparator { get; set; }

        public Dictionary<string, List<string>>? Phrases { get; set; }

        public Dictionary<string, string>? Zones { get; set; }

        public Dictionary<string, string>? Spots { get; set; }

        public Dictionary<string, string>? Sides { get; set; }

        public Dictionary<string, string>? Kits { get; set; }

        public Dictionary<string, string>? Teams { get; set; }

        public Dictionary<string, string>? Units { get; set; }

        public Dictionary<string, string>? Clock { get; set; }

        public Dictionary<string, string>? Score { get; set; }
    }
}
