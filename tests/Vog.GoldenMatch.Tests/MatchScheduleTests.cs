using Vog.Pipeline;

namespace Vog.GoldenMatch.Tests;

/// <summary>Every gateway works out the same match moment from the wall clock alone.</summary>
public sealed class MatchScheduleTests
{
    private static readonly MatchSchedule Schedule = new(MatchLengthMs: 95 * 60_000, Break: TimeSpan.FromMinutes(2), Speed: 1.0);

    [Fact]
    public void KickOffIsAtTheEpoch()
    {
        Assert.Equal(0, Schedule.At(MatchSchedule.Epoch).MatchMs);
    }

    [Fact]
    public void MidMatchIsTheTimeSinceKickOff()
    {
        Assert.Equal(10 * 60_000, Schedule.At(MatchSchedule.Epoch.AddMinutes(10)).MatchMs);
    }

    [Fact]
    public void TheBreakSaysWhenTheNextMatchStarts()
    {
        var (matchMs, untilKickOff) = Schedule.At(MatchSchedule.Epoch.AddMinutes(95.5));

        Assert.Null(matchMs);
        Assert.Equal(TimeSpan.FromMinutes(1.5), untilKickOff);
    }

    [Fact]
    public void MatchesRepeatEveryCycle()
    {
        var later = MatchSchedule.Epoch + (Schedule.Cycle * 40) + TimeSpan.FromMinutes(3);

        Assert.Equal(3 * 60_000, Schedule.At(later).MatchMs);
    }

    [Fact]
    public void FasterSpeedsCoverMoreMatchPerMinute()
    {
        var fast = Schedule with { Speed = 4.0 };

        Assert.Equal(40 * 60_000, fast.At(MatchSchedule.Epoch.AddMinutes(10)).MatchMs);
    }

    [Fact]
    public void TheGoldenMatchLastsAFullMatch()
    {
        var length = TimeSpan.FromMilliseconds(MatchSchedule.LengthOf(Vog.Pipeline.GoldenMatch.Setup(Vog.Pipeline.GoldenMatch.DefaultMatchSeed)));

        Assert.InRange(length.TotalMinutes, 90, 105);
    }
}
