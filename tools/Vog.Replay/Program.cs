using Vog.Replay;

var command = args.Length > 0 ? args[0] : "help";
var options = Options.Parse(args.Skip(1).ToArray());

switch (command)
{
    case "datacard":
        return DataCard.Run(options.Int("seeds", 20), options.ULong("seed", 1));

    case "commentary":
        return Commentary.Run(options);

    case "speak":
        return await Speak.RunAsync(options);

    case "listen":
        return await Listen.RunAsync(options);

    default:
        Console.WriteLine("vog-replay: offline tools for the Voice of the Game pipeline");
        Console.WriteLine();
        Console.WriteLine("  datacard   [--seeds 20] [--seed 1]                realism report over many seeded matches");
        Console.WriteLine("  commentary [--seed N] [--lang en-GB|es-ES] [--mode ad|commentary|calm|analyst|casual|kids]");
        Console.WriteLine("             [--from MIN] [--to MIN] [--minutes MIN] [--verbose] [--quiet]");
        Console.WriteLine("                                                    play a match as commentary + coverage report");
        Console.WriteLine("  speak      [--lang en-GB] [--mode ad] [--from MIN] [--minutes 2] [--out out/audio]");
        Console.WriteLine("                                                    render a window with Azure neural TTS: WAV + WebVTT");
        Console.WriteLine("  listen     [--url http://localhost:5080] [--lang en-GB] [--mode AudioDescription] [--seconds 30]");
        Console.WriteLine("                                                    smoke-test a running gateway as a browser listener would");
        return command == "help" ? 0 : 1;
}

internal sealed class Options(Dictionary<string, string> values)
{
    public static Options Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            values[key] = hasValue ? args[++i] : "true";
        }

        return new Options(values);
    }

    public string String(string key, string fallback) => values.TryGetValue(key, out var v) ? v : fallback;

    public int Int(string key, int fallback) => values.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    public ulong ULong(string key, ulong fallback) => values.TryGetValue(key, out var v) && ulong.TryParse(v, out var n) ? n : fallback;

    public bool Flag(string key) => values.ContainsKey(key);
}
