namespace Vog.Gateway;

/// <summary>
/// The Azure AI resource the gateway speaks through and the Foundry project its agents live in.
/// All are required: the gateway refuses to start without them rather than run in a reduced mode.
/// Locally they come from .env.local (written by infra/provision.ps1); in Azure, from app settings.
/// </summary>
public sealed class AzureAiOptions
{
    /// <summary>Speech (neural TTS): the resource's cognitiveservices endpoint.</summary>
    public Uri? SpeechEndpoint { get; set; }

    /// <summary>Voice Live: the resource's services.ai endpoint.</summary>
    public Uri? VoiceLiveEndpoint { get; set; }

    /// <summary>The Foundry project that holds the agents.</summary>
    public Uri? ProjectEndpoint { get; set; }

    /// <summary>The project's name: the last segment of its endpoint (…/api/projects/vog-dev).</summary>
    public string ProjectName => ProjectEndpoint?.Segments[^1].TrimEnd('/') ?? string.Empty;

    public static void Bind(AzureAiOptions options, IConfiguration configuration)
    {
        options.SpeechEndpoint = UriOrNull(configuration["VOG_AI_ENDPOINT"]);
        options.VoiceLiveEndpoint = UriOrNull(configuration["VOG_AI_SERVICES_ENDPOINT"]);
        options.ProjectEndpoint = UriOrNull(configuration["VOG_FOUNDRY_PROJECT_ENDPOINT"]);
    }

    public static bool IsComplete(AzureAiOptions o) => o.SpeechEndpoint is not null && o.VoiceLiveEndpoint is not null && o.ProjectEndpoint is not null;

    public const string Missing =
        "VOG_AI_ENDPOINT, VOG_AI_SERVICES_ENDPOINT and VOG_FOUNDRY_PROJECT_ENDPOINT must be set. " +
        "Run infra/provision.ps1, which writes them to .env.local.";

    private static Uri? UriOrNull(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri : null;
}
