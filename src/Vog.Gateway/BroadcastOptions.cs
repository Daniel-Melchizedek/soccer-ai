using Vog.Contracts;
using Vog.Pipeline;

namespace Vog.Gateway;

public sealed class BroadcastOptions
{
    public const string Section = "Broadcast";

    public ulong Seed { get; set; } = GoldenMatch.DefaultMatchSeed;

    /// <summary>Presentation rate: 1.0 follows the natural match clock.</summary>
    public double Speed { get; set; } = 1.0;

    public double BreakSeconds { get; set; } = 120;

    public bool Speak(IntentKind kind) => Speed <= 4 || kind is IntentKind.Goal or IntentKind.PeriodChange;

    /// <summary>Channels as "lang:Mode", for example "en-GB:AudioDescription".</summary>
    public List<string> Channels { get; set; } = [];

    public IReadOnlyList<ChannelSpec> ChannelSpecs() =>
        Channels.Select(c => c.Split(':', 2)).Select(p => new ChannelSpec(p[0], Enum.Parse<ListenerMode>(p[1], ignoreCase: true))).ToList();
}
