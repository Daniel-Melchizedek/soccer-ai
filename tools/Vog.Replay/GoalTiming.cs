using System.Text.Json;
using Vog.Contracts;
using Vog.Pipeline;
using Vog.Simulator;

namespace Vog.Replay;

internal static class GoalTiming
{
    private sealed record Goal(long PtsMs, int HomeScore, int AwayScore);

    public static int Run(int seeds, ulong firstSeed)
    {
        if (seeds is < 1 or > 100 || firstSeed > ulong.MaxValue - (ulong)(seeds - 1))
        {
            Console.Error.WriteLine("goaltiming requires 1..100 seeds and a range within UInt64.");
            return 1;
        }

        long? bestSpan = null;
        ulong? bestSeed = null;
        var kickoffCandidates = 0;
        var windowCandidates = 0;
        for (var index = 0; index < seeds; index++)
        {
            var seed = firstSeed + (ulong)index;
            var engine = new MatchEngine(GoldenMatch.Setup(seed));
            long? kickoff = null;
            var goals = new List<Goal>();
            foreach (var tick in engine.Run())
            {
                foreach (var matchEvent in tick.Events)
                {
                    if (matchEvent.Type == MatchEventType.KickOff)
                    {
                        kickoff ??= tick.Frame.PtsMs;
                    }
                    else if (matchEvent.Type == MatchEventType.Goal)
                    {
                        goals.Add(new Goal(tick.Frame.PtsMs, engine.HomeScore, engine.AwayScore));
                    }
                }
            }

            var goalsFromStart = goals.Count(goal => goal.PtsMs < 120_000);
            var goalsFromKickoff = kickoff is { } start
                ? goals.Count(goal => goal.PtsMs >= start && goal.PtsMs < start + 120_000)
                : 0;
            long? shortestTriple = null;
            for (var goalIndex = 2; goalIndex < goals.Count; goalIndex++)
            {
                var span = goals[goalIndex].PtsMs - goals[goalIndex - 2].PtsMs;
                shortestTriple = shortestTriple is null ? span : Math.Min(shortestTriple.Value, span);
            }

            if (goalsFromKickoff >= 3)
            {
                kickoffCandidates++;
            }
            if (shortestTriple is < 120_000)
            {
                windowCandidates++;
            }
            if (shortestTriple is { } candidate && (bestSpan is null || candidate < bestSpan))
            {
                bestSpan = candidate;
                bestSeed = seed;
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Seed = seed,
                FirstKickoffPtsMs = kickoff,
                Goals = goals,
                GoalsWithin120SecondsFromSimulationStart = goalsFromStart,
                GoalsWithin120SecondsFromFirstKickoff = goalsFromKickoff,
                ShortestThreeGoalSpanMs = shortestTriple,
            }));
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Seeds = seeds,
            FirstSeed = firstSeed,
            LastSeed = firstSeed + (ulong)seeds - 1,
            KickoffCandidates = kickoffCandidates,
            LaterWindowCandidates = windowCandidates,
            BestSeed = bestSeed,
            BestThreeGoalSpanMs = bestSpan,
            Rate = 1,
        }));
        return 0;
    }
}