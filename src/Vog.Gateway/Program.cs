using Azure.Storage.Blobs;
using Vog.Gateway;
using Vog.Voice;

var builder = WebApplication.CreateBuilder(args);

// Endpoints written by infra/provision.ps1 (git-ignored; no keys: the AI resource uses Entra ID only).
LocalSettings.AddDotEnv(builder.Configuration, builder.Environment.ContentRootPath);

// Secrets (the MCP key) come from Key Vault only: locally with your Azure CLI sign-in, in Azure with
// the app's managed identity. Secret names map to settings with "--" for ":" (Mcp--ApiKey).
var vault = builder.Configuration["VOG_KEY_VAULT_URI"] is { Length: > 0 } uri
    ? new Uri(uri)
    : throw new InvalidOperationException("VOG_KEY_VAULT_URI is not set. Run infra/provision.ps1, which writes it to .env.local.");
builder.Configuration.AddAzureKeyVault(vault, AzureCredentials.Shared);

builder.Services.Configure<BroadcastOptions>(builder.Configuration.GetSection(BroadcastOptions.Section));
builder.Services.AddOptions<AzureAiOptions>()
    .Configure<IConfiguration>(AzureAiOptions.Bind)
    .Validate(AzureAiOptions.IsComplete, AzureAiOptions.Missing)
    .ValidateOnStart();
builder.Services.AddSingleton<ChannelDirectory>();
builder.Services.AddSingleton<LatencyStats>();
builder.Services.AddSingleton<MatchArchiveService>();
builder.Services.AddSingleton<LiveBroadcastService>();
builder.Services.AddSingleton<CompanionRelay>();
builder.Services.AddSingleton<ReadoutService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveBroadcastService>());
builder.Services.AddSignalR();
builder.Services.AddMatchMcp(builder.Configuration);

var app = builder.Build();

app.MapMatchMcp();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<CommentaryHub>("/hubs/commentary");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/stats", (LatencyStats stats, ChannelDirectory directory) => Results.Ok(new { listeners = directory.ListenerCounts(), latency = stats.Snapshot() }));

// Recent match history from Azure Blob Storage. Returns an empty array when storage is not configured.
app.MapGet("/history", async (MatchArchiveService archive, CancellationToken ct) =>
{
    var summaries = await archive.GetRecentAsync(10, ct);
    return Results.Ok(summaries);
});

// Pre-aggregated historical football data (Kaggle CC0, proxied from Azure Blob Storage).
// Only serves the two allowed files to prevent arbitrary blob reads.
app.MapGet("/data/{file}", async (string file, IConfiguration config, CancellationToken ct) =>
{
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "goals-per-decade.json", "home-advantage.json" };
    if (!allowed.Contains(file)) return Results.NotFound();

    var endpoint = config["VogStorage__BlobEndpoint"];
    if (string.IsNullOrWhiteSpace(endpoint)) return Results.NotFound();

    try
    {
        Azure.Core.TokenCredential cred = AzureCredentials.Shared;
        var blobUri = new Uri(endpoint.TrimEnd('/') + $"/vog-data/public/{file}");
        var blobClient = new BlobClient(blobUri, cred);
        var response = await blobClient.DownloadContentAsync(ct);
        return Results.Content(response.Value.Content.ToString(), "application/json");
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not serve /data/{File} from blob storage", file);
        return Results.NotFound();
    }
});

app.Run();
