namespace Vog.Domain;

/// <summary>Football clock formatting: the second half starts at 45:00 and added time shows as "45+2".</summary>
public static class MatchClock
{
    public const long HalfMs = 45 * 60_000;

    public static long PeriodStartMs(int period) => period == 1 ? 0 : HalfMs;

    public static string Display(int period, long clockMs)
    {
        var regulationEnd = period == 1 ? HalfMs : 2 * HalfMs;
        if (clockMs > regulationEnd)
        {
            var added = (int)((clockMs - regulationEnd - 1) / 60_000) + 1;
            return $"{regulationEnd / 60_000}+{added}";
        }

        var totalSeconds = clockMs / 1000;
        return $"{totalSeconds / 60:D2}:{totalSeconds % 60:D2}";
    }

    /// <summary>The minute a commentator would say ("23 minutes gone"), and added minutes if any.</summary>
    public static (int Minute, int Added) Minute(int period, long clockMs)
    {
        var regulationEnd = period == 1 ? HalfMs : 2 * HalfMs;
        if (clockMs > regulationEnd)
        {
            return ((int)(regulationEnd / 60_000), (int)((clockMs - regulationEnd - 1) / 60_000) + 1);
        }

        return ((int)(clockMs / 60_000), 0);
    }
}
