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
builder.Services.AddSingleton<LiveBroadcastService>();
builder.Services.AddSingleton<CompanionRelay>();
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

app.Run();
