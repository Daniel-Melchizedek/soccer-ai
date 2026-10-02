using Vog.Contracts;

namespace Vog.Gateway;

/// <summary>
/// How late a line may start, behind the booth's schedule, before it's skipped. Like a human
/// describer, the channel drops a stale pass to catch up, but never a goal. The listener page
/// applies the same limits to its playback queue (wwwroot/js/app.js).
/// </summary>
public static class Staleness
{
    public static TimeSpan? SkipAfter(Priority priority) => priority switch
    {
        Priority.P0 => null,
        Priority.P1 => TimeSpan.FromSeconds(4),
        _ => TimeSpan.FromSeconds(1.5),
    };
}
