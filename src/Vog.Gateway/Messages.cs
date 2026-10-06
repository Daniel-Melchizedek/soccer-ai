using Vog.Contracts;
using Vog.Describe;
using Vog.Domain;
using Vog.Domain.Models;
using Vog.Pipeline;
using Vog.Voice;

namespace Vog.Gateway;

/// <summary>Sent when the booth starts an utterance on a channel (before its audio arrives).</summary>
/// <param name="Pan">Stereo position of the action, -1 (your left) to 1 (your right), for earcons.</param>
public sealed record UtteranceMessage(
    string Id,
    string Text,
    string Kind,
    string Priority,
    string? Earcon,
    string? Sound,
    string? Brief,
    double? Pan,
    string Clock,
    long ScheduledMs)
{
    public static UtteranceMessage From(Utterance u, TrackingFrame frame, string lang) =>
        new(
            u.UtteranceId,
            u.Text,
            u.Intent.Kind.ToString(),
            u.Intent.Priority.ToString(),
            u.Intent.EarconId,
            SoundFor(u, lang),
            BrailleBrief.Format(u, frame, new Realizer(PhraseBankLoader.Get(lang))),
            u.Intent.Spatial is { } p ? Math.Round(p.X / Pitch.Length * 2 - 1, 2) : null,
            MatchClock.Display(frame.Period, frame.MatchClockMs),
            u.DurationMs);

    private static string? SoundFor(Utterance u, string lang)
    {
        var key = u.Intent.Kind switch
        {
            IntentKind.Goal => "crowd_roar",
            IntentKind.Shot or IntentKind.ShotOutcome => "crowd_gasp",
            _ when u.Intent.EarconId == "whistle" => "whistle",
            _ => null,
        };

        return key is null ? null : PhraseBankLoader.Get(lang).Sounds.GetValueOrDefault(key);
    }
}

public sealed record CaptionCueMessage(long StartMs, long EndMs, IReadOnlyList<string> Lines)
{
    public static CaptionCueMessage From(CaptionCue cue) => new(cue.StartMs, cue.EndMs, cue.Lines);
}

public sealed record StateMessage(string Clock, int Period, int HomeScore, int AwayScore, bool BallInPlay);

/// <summary>
/// The pitch five times a second, for the ball sound, vibration, crowd meter and pitch map. Compact
/// on purpose (every listener gets it): the ball as [x, y, height] in metres, who has it ("h", "a" or
/// null), whether the home team attacks towards increasing x, and each side's players as
/// [x, y, shirt number]. Positions are rounded to 0.1 m.
/// </summary>
public sealed record PitchMessage(long T, int P, double[] B, string? O, bool Hr, double E, double[][] H, double[][] A)
{
    public static PitchMessage From(MatchSnapshot s)
    {
        var home = s.Info.Home.TeamId;
        var frame = s.Frame;
        string? possession = s.Ball.PossessionTeamId is not { } team ? null : team == home ? "h" : "a";
        var attackingRight = possession switch
        {
            "h" => s.Info.AttacksRight(home, frame.Period),
            "a" => !s.Info.AttacksRight(home, frame.Period),
            _ => (bool?)null,
        };
        var threat = attackingRight is { } direction
            ? Math.Clamp((ThreatModel.Value(frame.Ball.Position, direction) - 0.008) / 0.32, 0, 1)
            : 0;
        var recentPulse = s.KeyEvents
            .Where(e => e.Period == frame.Period && e.Type is MatchEventType.Shot or MatchEventType.Goal or MatchEventType.Save)
            .Select(e => (frame.MatchClockMs - e.MatchClockMs, e.Type))
            .Where(e => e.Item1 is >= 0 and <= 14_000)
            .Select(e => (e.Type == MatchEventType.Goal ? 1.0 : e.Type == MatchEventType.Shot ? 0.8 : 0.55) * Math.Exp(-e.Item1 / 7000.0))
            .DefaultIfEmpty(0)
            .Max();
        var energy = Math.Clamp(Math.Max(threat, recentPulse), 0, 1);
        double[][] Side(bool isHome) => frame.Players
            .Where(p => (p.TeamId == home) == isHome)
            .Select(p => new[] { Round(p.X), Round(p.Y), p.Jersey })
            .ToArray();

        return new(
            frame.PtsMs,
            frame.Period,
            [Round(frame.Ball.X), Round(frame.Ball.Y), Round(frame.Ball.Z)],
            possession,
            s.Info.AttacksRight(home, frame.Period),
            Math.Round(energy, 3),
            Side(isHome: true),
            Side(isHome: false));
    }

    private static double Round(double metres) => Math.Round(metres, 1);
}

public sealed record JoinResult(string ChannelId, string Home, string HomeKit, string Away, string AwayKit);
