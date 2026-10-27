namespace Vog.Gateway.Tests;

public sealed class MatchArchiveServiceTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.Parse("2026-10-26T00:00:00Z");

    private static MatchSummary Summary(ulong seed, string home = "Home", string away = "Away") =>
        new(seed, Epoch, 0, 1, home, away, [], 5689500);

    [Fact]
    public void RepeatedSeededFixtureKeepsNewestArchive()
    {
        var older = Summary(20261026);
        var newest = older with { UtcDate = Epoch.AddHours(1), AwayScore = 2 };
        var results = MatchArchiveService.SelectRecent([
            (Epoch, older), (Epoch.AddHours(1), newest), (Epoch.AddMinutes(30), older)
        ], 10);

        Assert.Same(newest, Assert.Single(results));
    }

    [Fact]
    public void DeduplicatesBeforeApplyingLimit()
    {
        var repeated = Summary(20261026);
        var different = Summary(20261027);
        var archives = Enumerable.Range(1, 20)
            .Select(index => (Epoch.AddMinutes(index), repeated))
            .Append((Epoch, different));

        var results = MatchArchiveService.SelectRecent(archives, 2);

        Assert.Equal(new[] { repeated, different }, results);
    }

    [Fact]
    public void DifferentSeedsAndTeamsRemainSeparateAndNewestFirst()
    {
        var first = Summary(1);
        var second = Summary(2);
        var third = Summary(1, "Other home");
        var fourth = Summary(1, away: "Other away");
        var results = MatchArchiveService.SelectRecent([
            (Epoch, first), (Epoch.AddMinutes(1), second),
            (Epoch.AddMinutes(2), third), (Epoch.AddMinutes(3), fourth)
        ], 10);

        Assert.Equal(new[] { fourth, third, second, first }, results);
    }

    [Fact]
    public void EmptyArchivesAndZeroLimitReturnEmptyHistory()
    {
        Assert.Empty(MatchArchiveService.SelectRecent([], 10));
        Assert.Empty(MatchArchiveService.SelectRecent([(Epoch, Summary(1))], 0));
    }
}