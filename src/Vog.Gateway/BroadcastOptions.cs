using Vog.Contracts;
using Vog.Pipeline;

namespace Vog.Gateway;

public sealed class BroadcastOptions
{
    public const string Section = "Broadcast";

    public ulong Seed { get; set; } = GoldenMatch.DefaultMatchSeed;

    /// <summary>1.0 is real time; higher speeds the match up for demos and tests.</summary>
    public double Speed { get; set; } = 1.0;

    /// <summary>Channels as "lang:Mode", for example "en-GB:AudioDescription".</summary>
    public List<string> Channels { get; set; } = [];

    public IReadOnlyList<ChannelSpec> ChannelSpecs() =>
        Channels.Select(c => c.Split(':', 2)).Select(p => new ChannelSpec(p[0], Enum.Parse<ListenerMode>(p[1], ignoreCase: true))).ToList();
}
