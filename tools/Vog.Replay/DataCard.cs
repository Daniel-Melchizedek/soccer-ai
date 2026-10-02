using Vog.Pipeline;
using Vog.Simulator;
using Vog.Stats;

namespace Vog.Replay;

/// <summary>
/// Synthetic data card: runs many seeded matches and checks realism against the ranges in
/// BUILD_PLAN.md §6.1 (pass completion, shots, goals, distance and sprints per outfield player).
/// </summary>
internal static class DataCard
{
    public sealed record MatchSummary(
        ulong Seed,
        int Goals,
        int Shots,
        int Passes,
        double PassCompletion,
        double AvgOutfieldKm,
        double AvgOutfieldSprints,
        double TopSpeedKmh,
        int Fouls,
        int Cards,
        int Corners,
        int Offsides,
        double Xg,
        double InPlayMinutes,
        int ThrowIns,
        int GoalKicks);

    public static MatchSummary RunMatch(ulong seed)
    {
        var setup = GoldenMatch.Setup(seed);
        var engine = new MatchEngine(setup);
        var stats = new StatsEngine(engine.Info);
        foreach (var tick in engine.Run())
        {
            stats.Process(tick);
        }

        stats.Flush();

        var t = stats.Totals;
        var goalkeepers = engine.Info.Home.Starters.Concat(engine.Info.Home.Bench).Concat(engine.Info.Away.Starters).Concat(engine.Info.Away.Bench)
            .Where(p => p.Role == Contracts.Role.Goalkeeper)
            .Select(p => p.PlayerId)
            .ToHashSet();

        // Outfield players who were on the pitch for most of the match (> 7 km covered or started and finished).
        var outfield = stats.Motion.Players.Values.Where(m => !goalkeepers.Contains(m.PlayerId) && m.DistanceM > 3000).ToList();

        return new MatchSummary(
            seed,
            t.Home.Goals + t.Away.Goals,
            t.Home.Shots + t.Away.Shots,
            t.Home.PassesAttempted + t.Away.PassesAttempted,
            (double)(t.Home.PassesCompleted + t.Away.PassesCompleted) / Math.Max(1, t.Home.PassesAttempted + t.Away.PassesAttempted),
            outfield.Count == 0 ? 0 : outfield.Average(m => m.DistanceM) / 1000,
            outfield.Count == 0 ? 0 : outfield.Average(m => m.Sprints),
            stats.Motion.Players.Values.Max(m => m.TopSpeedMs) * 3.6,
            t.Home.Fouls + t.Away.Fouls,
            t.Home.YellowCards + t.Away.YellowCards + t.Home.RedCards + t.Away.RedCards,
            t.Home.Corners + t.Away.Corners,
            t.Home.Offsides + t.Away.Offsides,
            t.Home.Xg + t.Away.Xg,
            t.InPlayMs / 60_000.0,
            t.Home.ThrowIns + t.Away.ThrowIns,
            t.Home.GoalKicks + t.Away.GoalKicks);
    }

    public static int Run(int seeds, ulong firstSeed)
    {
        var results = new List<MatchSummary>();
        for (var i = 0; i < seeds; i++)
        {
            results.Add(RunMatch(firstSeed + (ulong)i));
        }

        Console.WriteLine($"Synthetic data card: {seeds} matches, seeds {firstSeed}..{firstSeed + (ulong)seeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"{"metric",-34} {"mean",8} {"min",8} {"max",8}   target");
        var checks = new (string Name, Func<MatchSummary, double> Value, double Lo, double Hi, string Target)[]
        {
            ("pass completion", r => r.PassCompletion * 100, 75, 88, "75–88 %"),
            ("passes per match", r => r.Passes, 750, 1150, "~750–1150"),
            ("ball in play (min)", r => r.InPlayMinutes, 52, 64, "52–64"),
            ("shots per match", r => r.Shots, 20, 30, "20–30"),
            ("goals per match", r => r.Goals, 2.2, 3.2, "~2.7 (mean)"),
            ("xG per match", r => r.Xg, 2.2, 3.4, "≈ goals"),
            ("xG per shot", r => r.Shots == 0 ? 0 : r.Xg / r.Shots, 0.08, 0.14, "0.08–0.14"),
            ("distance per outfield player (km)", r => r.AvgOutfieldKm, 9, 12, "9–12 km"),
            ("sprints per outfield player", r => r.AvgOutfieldSprints, 20, 40, "20–40"),
            ("top speed (km/h)", r => r.TopSpeedKmh, 31, 36.5, "31–36.5"),
            ("fouls per match", r => r.Fouls, 14, 30, "14–30"),
            ("cards per match", r => r.Cards, 1.5, 6, "1.5–6"),
            ("corners per match", r => r.Corners, 6, 14, "6–14"),
            ("offsides per match", r => r.Offsides, 1, 7, "1–7"),
            ("throw-ins per match", r => r.ThrowIns, 30, 50, "30–50"),
            ("goal kicks per match", r => r.GoalKicks, 12, 24, "12–24"),
        };

        var failures = 0;
        foreach (var (name, value, lo, hi, target) in checks)
        {
            var values = results.Select(value).ToList();
            var mean = values.Average();
            var ok = mean >= lo && mean <= hi;
            failures += ok ? 0 : 1;
            Console.WriteLine($"{name,-34} {mean,8:0.0} {values.Min(),8:0.0} {values.Max(),8:0.0}   {target} {(ok ? "ok" : "OUT OF RANGE")}");
        }

        Console.WriteLine();
        Console.WriteLine(failures == 0 ? "All realism checks passed." : $"{failures} realism check(s) out of range.");
        return failures == 0 ? 0 : 1;
    }
}
