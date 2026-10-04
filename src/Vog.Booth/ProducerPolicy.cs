using Vog.Contracts;

namespace Vog.Booth;

/// <summary>
/// How a channel behaves (BUILD_PLAN.md §7.2). Audio description mentions the ball's location at
/// least every 4 seconds in open play; Calm mode is slower and less frequent.
/// </summary>
/// <param name="WordsPerSecond">
/// Speaking-rate estimate used to schedule before audio exists. Calibrated against Azure neural
/// voices with `vog-replay speak` (spoken ÷ estimated duration ≈ 1.0 at the median). Once audio is
/// synthesized at enqueue time, its real duration replaces the estimate.
/// </param>
/// <param name="PassByPass">
/// Whether routine pass and carry calls are spoken. Calm mode leaves them out for a steadier,
/// less busy cadence: possession changes, chances, goals and regular location calls remain.
/// </param>
public sealed record ProducerPolicy(
    ListenerMode Mode,
    long BallLocationMaxGapMs,
    long ScoreTimeIntervalMs,
    bool ReadGraphics,
    double EnergyCap,
    double WordsPerSecond,
    bool PassByPass = true)
{
    public static ProducerPolicy For(ListenerMode mode, string lang = "en-GB")
    {
        var policy = mode switch
        {
            ListenerMode.AudioDescription => new ProducerPolicy(mode, 4000, 180_000, ReadGraphics: true, EnergyCap: 0.7, WordsPerSecond: 3.3),
            ListenerMode.Calm => new ProducerPolicy(mode, 6000, 180_000, ReadGraphics: false, EnergyCap: 0.3, WordsPerSecond: 2.3, PassByPass: false),
            ListenerMode.Analyst => new ProducerPolicy(mode, 10000, 300_000, ReadGraphics: true, EnergyCap: 0.6, WordsPerSecond: 2.7),
            ListenerMode.Kids => new ProducerPolicy(mode, 6000, 180_000, ReadGraphics: true, EnergyCap: 0.8, WordsPerSecond: 2.4),
            _ => new ProducerPolicy(mode, 8000, 300_000, ReadGraphics: false, EnergyCap: 1.0, WordsPerSecond: 2.7),
        };

        return policy with { WordsPerSecond = policy.WordsPerSecond * LanguageRate(lang) };
    }

    /// <summary>
    /// Words per second relative to English, calibrated against the live voices with
    /// <c>vog-replay speak</c>: Spanish words carry more syllables, and Tamil words, which take
    /// their case endings as suffixes, are longer still (spoken ÷ estimated was 1.27 at 1.0).
    /// </summary>
    private static double LanguageRate(string lang) => lang[..2].ToLowerInvariant() switch
    {
        "es" => 0.92,
        "ta" => 0.79,
        _ => 1.0,
    };
}
