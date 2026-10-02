using Vog.Contracts;
using Vog.Domain;
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

public sealed record JoinResult(string ChannelId, string Home, string HomeKit, string Away, string AwayKit, bool Speech);
