using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Physical;

namespace Vog.Gateway;

internal static class LocalSettings
{
    private const string FileName = ".env.local";

    /// <summary>
    /// Adds the repository's .env.local (KEY=VALUE lines, which the INI provider reads as-is) if one
    /// exists above the content root. Environment variables and appsettings still take precedence
    /// for anything set there too; in Azure the file doesn't exist and settings come from the app.
    /// </summary>
    public static void AddDotEnv(ConfigurationManager configuration, string contentRoot)
    {
        for (var dir = new DirectoryInfo(contentRoot); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, FileName)))
            {
                // The default file provider hides dot-prefixed files, so name none as excluded.
                var files = new PhysicalFileProvider(dir.FullName, ExclusionFilters.None);
                configuration.AddIniFile(files, FileName, optional: false, reloadOnChange: false);
                configuration.AddEnvironmentVariables();
                return;
            }
        }
    }
}
