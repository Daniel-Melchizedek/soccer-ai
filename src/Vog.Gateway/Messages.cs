using Vog.Contracts;
using Vog.Domain;
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
    double? Pan,
    string Clock,
    long ScheduledMs)
{
    public static UtteranceMessage From(Utterance u, TrackingFrame frame) =>
        new(
            u.UtteranceId,
            u.Text,
            u.Intent.Kind.ToString(),
            u.Intent.Priority.ToString(),
            u.Intent.EarconId,
            u.Intent.Spatial is { } p ? Math.Round(p.X / Pitch.Length * 2 - 1, 2) : null,
            MatchClock.Display(frame.Period, frame.MatchClockMs),
            u.DurationMs);
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
public sealed record PitchMessage(long T, int P, double[] B, string? O, bool Hr, double[][] H, double[][] A)
{
    public static PitchMessage From(MatchSnapshot s)
    {
        var home = s.Info.Home.TeamId;
        var frame = s.Frame;
        string? possession = s.Ball.PossessionTeamId is not { } team ? null : team == home ? "h" : "a";
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
            Side(isHome: true),
            Side(isHome: false));
    }

    private static double Round(double metres) => Math.Round(metres, 1);
}

public sealed record JoinResult(string ChannelId, string Home, string HomeKit, string Away, string AwayKit);
