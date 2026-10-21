using Vog.Contracts;
using Vog.Pipeline;

namespace Vog.Gateway;

public sealed class BroadcastOptions
{
    public const string Section = "Broadcast";

    public ulong Seed { get; set; } = 3;

    /// <summary>Presentation rate: 60.0 runs one match minute per wall-clock second.</summary>
    public double Speed { get; set; } = 60.0;

    public double BreakSeconds { get; set; }

    public bool Speak(IntentKind kind) => Speed <= 4 || kind is IntentKind.Goal or IntentKind.PeriodChange;

    /// <summary>Channels as "lang:Mode", for example "en-GB:AudioDescription".</summary>
    public List<string> Channels { get; set; } = [];

    public IReadOnlyList<ChannelSpec> ChannelSpecs() =>
        Channels.Select(c => c.Split(':', 2)).Select(p => new ChannelSpec(p[0], Enum.Parse<ListenerMode>(p[1], ignoreCase: true))).ToList();
}
