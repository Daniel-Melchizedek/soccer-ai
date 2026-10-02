using Vog.Contracts;
using Vog.Stats;

namespace Vog.Describe;

/// <summary>
/// Builds the intents the Booth Producer asks for to meet its obligations: "where's the ball"
/// mentions in open play, and regular score-and-time reminders.
/// </summary>
public sealed class ReminderFactory(MatchInfo info)
{
    private readonly SlotFactory _slots = new(info);
    private long _seq;

    public UtteranceIntent? BallLocation(BallState ball, long ptsMs)
    {
        if (ball.Zone is not { } zone || ball.PossessionTeamId is not { } teamId)
        {
            return null;
        }

        var slots = new Dictionary<string, Slot> { ["team"] = _slots.Team(teamId), ["zone"] = new ZoneSlot(zone) };
        var key = "loc.ball.loose";
        if (ball.CarrierId is { } carrier)
        {
            slots["player"] = _slots.Player(carrier);
            key = "loc.ball";
        }

        return Create(IntentKind.BallLocation, key, slots, ptsMs, validMs: 2000, mentionsLocation: true, supersede: "loc", spatial: ball.Position);
    }

    public UtteranceIntent ScoreTime(BallState ball, long ptsMs) =>
        Create(IntentKind.ScoreTime, "rem.score_time",
            new Dictionary<string, Slot> { ["clock"] = _slots.Clock(ball.Period, ball.MatchClockMs), ["score"] = _slots.Score(ball) },
            ptsMs, validMs: 10000, mentionsLocation: false, supersede: "rem", spatial: null);

    private UtteranceIntent Create(
        IntentKind kind,
        string key,
        IReadOnlyDictionary<string, Slot> slots,
        long ptsMs,
        long validMs,
        bool mentionsLocation,
        string supersede,
        PitchPoint? spatial) =>
        new(
            IntentId: $"{info.MatchId}-r{++_seq:D6}",
            MatchId: info.MatchId,
            Kind: kind,
            TemplateKey: key,
            Slots: slots,
            Priority: Priority.P2,
            Value: 0.5,
            PtsMs: ptsMs,
            ValidUntilMs: ptsMs + validMs,
            MentionsBallLocation: mentionsLocation,
            SupersedeKey: supersede,
            Energy: 0.35,
            Spatial: spatial,
            Source: IntentSource.Producer);
}
