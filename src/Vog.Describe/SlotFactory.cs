using Vog.Contracts;
using Vog.Domain;
using Vog.Stats;

namespace Vog.Describe;

/// <summary>Builds typed slots from match metadata, so describers never format text themselves.</summary>
internal sealed class SlotFactory(MatchInfo info)
{
    public MatchInfo Info => info;

    public PlayerSlot Player(string? playerId) =>
        new(playerId ?? string.Empty, info.FindPlayer(playerId)?.ShortName ?? "a player");

    public TeamSlot Team(string teamId)
    {
        var t = info.Team(teamId);
        return new TeamSlot(t.TeamId, t.Nickname, t.KitColour);
    }

    public string OpponentId(string teamId) => info.Opponent(teamId).TeamId;

    /// <summary>Zone from the point of view of <paramref name="teamId"/> attacking its goal.</summary>
    public ZoneSlot Zone(PitchPoint p, string teamId, int period) => new(Pitch.ZoneFor(p, info.AttacksRight(teamId, period)));

    /// <summary>The listener sits in the main stand: the goal at X = 0 is on their left.</summary>
    public SideSlot AttackingSide(string teamId, int period) => new(Left: !info.AttacksRight(teamId, period));

    public ScoreSlot Score(BallState ball) =>
        new(Team(info.Home.TeamId), ball.HomeScore, Team(info.Away.TeamId), ball.AwayScore);

    public ClockSlot Clock(int period, long matchClockMs)
    {
        var (minute, added) = MatchClock.Minute(period, matchClockMs);
        return new ClockSlot(minute, added);
    }

    public static NumberSlot Number(double value, string unit) => new(value, unit);
}
