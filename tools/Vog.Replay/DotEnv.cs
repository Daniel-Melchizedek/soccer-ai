namespace Vog.Replay;

/// <summary>
/// Loads the non-secret settings that infra/provision.ps1 writes to .env.local (git-ignored).
/// Variables already set in the environment win.
/// </summary>
internal static class DotEnv
{
    public static string? Load(string fileName = ".env.local")
    {
        foreach (var directory in Candidates())
        {
            var path = Path.Combine(directory, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                var eq = line.IndexOf('=', StringComparison.Ordinal);
                if (line.Length == 0 || line.StartsWith('#') || eq <= 0)
                {
                    continue;
                }

                var key = line[..eq].Trim();
                if (Environment.GetEnvironmentVariable(key) is null)
                {
                    Environment.SetEnvironmentVariable(key, line[(eq + 1)..].Trim());
                }
            }

            return path;
        }

        return null;
    }

    private static IEnumerable<string> Candidates()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                yield return dir.FullName;
            }
        }
    }
}
