using System.Text.RegularExpressions;
using Vog.Contracts;
using Vog.Pipeline;
using Vog.Simulator;
using Vog.Stats;

namespace Vog.Describe.Tests;

public sealed partial class PhraseBankTests
{
    private static readonly IReadOnlyDictionary<string, Slot> SampleSlots = new Dictionary<string, Slot>
    {
        ["player"] = new PlayerSlot("p1", "Okafor"),
        ["receiver"] = new PlayerSlot("p2", "Mensah"),
        ["victim"] = new PlayerSlot("p3", "Silva"),
        ["keeper"] = new PlayerSlot("p4", "Kova"),
        ["replaced"] = new PlayerSlot("p5", "Duarte"),
        ["team"] = new TeamSlot("c-00", "Reds", "red"),
        ["zone"] = new ZoneSlot(new Zone(PitchThird.Middle, PitchChannel.Left)),
        ["side"] = new SideSlot(Left: true),
        ["distance"] = new NumberSlot(14, "m"),
        ["speed"] = new NumberSlot(33.1, "kmh"),
        ["score"] = new ScoreSlot(new TeamSlot("c-00", "Reds", "red"), 1, new TeamSlot("c-01", "Blues", "blue"), 0),
        ["clock"] = new ClockSlot(23, 0),
    };

    public static TheoryData<string> Languages => ["en-GB", "es-ES"];

    [Fact]
    public void BanksLoadWithTheExpectedReviewStatus()
    {
        Assert.Equal(PhraseStatus.Reviewed, PhraseBankLoader.Get("en-GB").Status);
        Assert.Equal(PhraseStatus.Draft, PhraseBankLoader.Get("es-ES").Status);
    }

    [Fact]
    public void SpanishCoversEveryEnglishKey()
    {
        var en = PhraseBankLoader.Get("en-GB");
        var es = PhraseBankLoader.Get("es-ES");

        Assert.Empty(en.Phrases.Keys.Except(es.Phrases.Keys));
        Assert.Empty(en.Zones.Keys.Except(es.Zones.Keys));
        Assert.Empty(en.Spots.Keys.Except(es.Spots.Keys));
        Assert.Empty(en.Kits.Keys.Except(es.Kits.Keys));
        Assert.Empty(en.Units.Keys.Except(es.Units.Keys));
        Assert.Empty(en.Clock.Keys.Except(es.Clock.Keys));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryVariantRealizesWithNoPlaceholderLeft(string lang)
    {
        var realizer = new Realizer(PhraseBankLoader.Get(lang));
        foreach (var (key, variants) in realizer.Bank.Phrases)
        {
            foreach (var variant in variants)
            {
                var text = realizer.Fill(variant, SampleSlots);
                Assert.DoesNotMatch(Placeholder(), text);
                Assert.False(string.IsNullOrWhiteSpace(text), key);
            }
        }
    }

    [Fact]
    public void ConsecutiveIntentsDoNotRepeatTheSameSentence()
    {
        var realizer = new Realizer(PhraseBankLoader.Get("en-GB"));
        string? previous = null;
        for (var i = 0; i < 50; i++)
        {
            var text = realizer.Realize(Intent($"i{i}", "pbp.pass.short"));
            Assert.NotEqual(previous, text);
            previous = text;
        }
    }

    [Fact]
    public void RealizationIsDeterministic()
    {
        var a = new Realizer(PhraseBankLoader.Get("en-GB"));
        var b = new Realizer(PhraseBankLoader.Get("en-GB"));
        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(a.Realize(Intent($"i{i}", "loc.ball")), b.Realize(Intent($"i{i}", "loc.ball")));
        }
    }

    [Fact]
    public void SpanishUsesItsOwnTeamNamesCapitalisationAndDecimalComma()
    {
        var realizer = new Realizer(PhraseBankLoader.Get("es-ES"));

        Assert.Equal("Arranca el partido. Los Rojos, de rojo, atacan hacia la portería de tu izquierda.", realizer.Realize(Intent("k", "pbp.kickoff")));
        Assert.Contains("33,1 kilómetros por hora", realizer.Realize(Intent("g", "graphic.top_speed")));
    }

    [Fact]
    public void ShotSpeedsReadAsWholeNumbers()
    {
        var realizer = new Realizer(PhraseBankLoader.Get("en-GB"));
        var slots = new Dictionary<string, Slot>(SampleSlots) { ["speed"] = new NumberSlot(104.4, "kmh") };
        Assert.Equal("On screen: shot speed, 104 kilometres an hour.", realizer.Fill("On screen: shot speed, {speed}.", slots));
    }

    [Fact]
    public void EveryIntentTheDescriberProducesExistsInBothBanks()
    {
        var setup = GoldenMatch.Setup();
        var engine = new MatchEngine(setup);
        var stats = new StatsEngine(engine.Info);
        var describer = new PlayByPlayDescriber(engine.Info);
        var keys = new HashSet<string> { "loc.ball", "loc.ball.loose", "rem.score_time" };
        foreach (var tick in engine.Run())
        {
            foreach (var intent in describer.Describe(stats.Process(tick)))
            {
                keys.Add(intent.TemplateKey);
            }
        }

        foreach (var lang in new[] { "en-GB", "es-ES" })
        {
            var bank = PhraseBankLoader.Get(lang);
            Assert.All(keys, k => Assert.True(bank.Phrases.ContainsKey(k), $"{lang} is missing '{k}'"));
        }
    }

    private static UtteranceIntent Intent(string id, string key) =>
        new(id, "m", IntentKind.PbpPass, key, SampleSlots, Priority.P2, 0.5, 0, 1000);

    [GeneratedRegex(@"\{[a-z_.]+\}")]
    private static partial Regex Placeholder();
}
