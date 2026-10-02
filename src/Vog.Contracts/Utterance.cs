namespace Vog.Contracts;

public enum ListenerMode
{
    AudioDescription,
    Commentary,
    Calm,
    Analyst,
    Casual,
    Kids,
}

public enum UtteranceStatus
{
    Done,
    Preempted,
    DroppedStale,
    DroppedSuperseded,
}

/// <summary>An intent rendered for one channel and scheduled on its audio timeline.</summary>
public sealed record Utterance(
    string UtteranceId,
    UtteranceIntent Intent,
    string ChannelId,
    string Text,
    long StartPtsMs,
    long EndPtsMs,
    UtteranceStatus Status)
{
    public long DurationMs => EndPtsMs - StartPtsMs;
}
