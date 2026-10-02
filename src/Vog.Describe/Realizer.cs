using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Vog.Contracts;

namespace Vog.Describe;

/// <summary>
/// Renders an <see cref="UtteranceIntent"/> into text for one language. Variant choice is
/// deterministic per intent, with a no-repeat rule per template key so commentary doesn't
/// say the same sentence twice in a row.
/// </summary>
public sealed partial class Realizer(PhraseBank bank)
{
    private readonly Dictionary<string, int> _lastVariant = [];

    public PhraseBank Bank => bank;

    public string Realize(UtteranceIntent intent)
    {
        if (!bank.Phrases.TryGetValue(intent.TemplateKey, out var variants) || variants.Count == 0)
        {
            throw new KeyNotFoundException($"Phrase bank {bank.Lang}/{bank.Persona} has no template '{intent.TemplateKey}'.");
        }

        var index = (int)(StableHash(intent.IntentId) % (uint)variants.Count);
        if (variants.Count > 1 && _lastVariant.TryGetValue(intent.TemplateKey, out var last) && last == index)
        {
            index = (index + 1) % variants.Count;
        }

        _lastVariant[intent.TemplateKey] = index;
        return Fill(variants[index], intent.Slots);
    }

    /// <summary>Replaces {slot} and {slot.kit} placeholders; throws if a slot is missing.</summary>
    public string Fill(string template, IReadOnlyDictionary<string, Slot> slots)
    {
        var text = Placeholder().Replace(template, m =>
        {
            var name = m.Groups["name"].Value;
            var property = m.Groups["prop"].Success ? m.Groups["prop"].Value : null;
            if (!slots.TryGetValue(name, out var slot))
            {
                throw new InvalidOperationException($"Template '{template}' needs slot '{name}', which the intent doesn't provide.");
            }

            return Format(slot, property);
        });

        return Capitalise(text);
    }

    public string Format(Slot slot, string? property = null) => slot switch
    {
        PlayerSlot p => p.Name,
        TeamSlot t when property == "kit" => Lookup(bank.Kits, t.KitColour, "kits"),
        TeamSlot t => TeamName(t),
        ZoneSlot z => ZonePhrase(z.Zone),
        NumberSlot n => Lookup(bank.Units, n.Unit, "units").Replace("{value}", FormatNumber(n), StringComparison.Ordinal),
        ClockSlot c => ClockPhrase(c),
        ScoreSlot s => Lookup(bank.Score, "format", "score")
            .Replace("{home}", TeamName(s.Home), StringComparison.Ordinal)
            .Replace("{homeGoals}", s.HomeGoals.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{away}", TeamName(s.Away), StringComparison.Ordinal)
            .Replace("{awayGoals}", s.AwayGoals.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal),
        SideSlot side => Lookup(bank.Sides, side.Left ? "left" : "right", "sides"),
        _ => throw new NotSupportedException($"Unknown slot type {slot.GetType().Name}."),
    };

    public string TeamName(TeamSlot team) =>
        bank.Teams.TryGetValue(team.KitColour, out var localised) ? localised : team.Nickname;

    private string ZonePhrase(Zone zone)
    {
        if (zone.Spot != PitchSpot.None)
        {
            return Lookup(bank.Spots, SnakeCase(zone.Spot.ToString()), "spots");
        }

        var key = $"{zone.Third.ToString().ToLowerInvariant()}.{zone.Channel.ToString().ToLowerInvariant()}";
        return Lookup(bank.Zones, key, "zones");
    }

    private string ClockPhrase(ClockSlot c)
    {
        var key = c.AddedMinute > 0 ? "added" : c.Minute == 0 ? "first_minute" : "regular";
        return Lookup(bank.Clock, key, "clock")
            .Replace("{minute}", c.Minute.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{added}", c.AddedMinute.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private string FormatNumber(NumberSlot n)
    {
        // Shot speeds read as whole numbers; sprint speeds and distances keep one decimal.
        var whole = n.Unit == "m" || (n.Unit == "kmh" && n.Value >= 50) || Math.Abs(n.Value - Math.Round(n.Value)) < 0.05;
        var text = whole
            ? Math.Round(n.Value).ToString("0", CultureInfo.InvariantCulture)
            : n.Value.ToString("0.0", CultureInfo.InvariantCulture);
        return text.Replace(".", bank.DecimalSeparator, StringComparison.Ordinal);
    }

    private string Lookup(IReadOnlyDictionary<string, string> table, string key, string tableName) =>
        table.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Phrase bank {bank.Lang} has no '{key}' in '{tableName}'.");

    private static string SnakeCase(string pascal)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < pascal.Length; i++)
        {
            if (char.IsUpper(pascal[i]) && i > 0)
            {
                sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(pascal[i]));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Upper-cases the first letter of every sentence (a slot such as "los Rojos" may start one),
    /// skipping opening punctuation such as "¡" and leaving a sentence that starts with a number alone.
    /// </summary>
    private static string Capitalise(string text)
    {
        var chars = text.ToCharArray();
        var sentenceStart = true;
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (sentenceStart)
            {
                if (char.IsLetter(c))
                {
                    chars[i] = char.ToUpperInvariant(c);
                    sentenceStart = false;
                }
                else if (char.IsDigit(c))
                {
                    sentenceStart = false;
                }

                continue;
            }

            if (c is '.' or '!' or '?' && i + 1 < chars.Length && char.IsWhiteSpace(chars[i + 1]))
            {
                sentenceStart = true;
            }
        }

        return new string(chars);
    }

    /// <summary>FNV-1a: stable across processes, unlike <see cref="string.GetHashCode()"/>.</summary>
    internal static uint StableHash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash ^= c;
            hash *= 16777619u;
        }

        return hash;
    }

    [GeneratedRegex(@"\{(?<name>[a-z_]+)(?:\.(?<prop>[a-z_]+))?\}")]
    private static partial Regex Placeholder();
}
