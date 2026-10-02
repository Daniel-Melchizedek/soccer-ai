using Vog.Contracts;

namespace Vog.Booth;

/// <summary>
/// How a channel behaves (BUILD_PLAN.md §7.2). Audio description mentions the ball's location at
/// least every 4 seconds in open play; Calm mode is slower and less frequent.
/// </summary>
public sealed record ProducerPolicy(
    ListenerMode Mode,
    long BallLocationMaxGapMs,
    long ScoreTimeIntervalMs,
    bool ReadGraphics,
    double EnergyCap,
    double WordsPerSecond)
{
    public static ProducerPolicy For(ListenerMode mode) => mode switch
    {
        ListenerMode.AudioDescription => new(mode, 4000, 180_000, ReadGraphics: true, EnergyCap: 0.7, WordsPerSecond: 3.3),
        ListenerMode.Calm => new(mode, 6000, 180_000, ReadGraphics: false, EnergyCap: 0.3, WordsPerSecond: 2.5),
        ListenerMode.Analyst => new(mode, 10000, 300_000, ReadGraphics: true, EnergyCap: 0.6, WordsPerSecond: 2.9),
        ListenerMode.Kids => new(mode, 6000, 180_000, ReadGraphics: true, EnergyCap: 0.8, WordsPerSecond: 2.5),
        _ => new(mode, 8000, 300_000, ReadGraphics: false, EnergyCap: 1.0, WordsPerSecond: 2.9),
    };
}
