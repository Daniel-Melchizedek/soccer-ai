using Vog.Gateway;

var builder = WebApplication.CreateBuilder(args);

// Endpoints written by infra/provision.ps1 (git-ignored; no keys: the AI resource uses Entra ID only).
LocalSettings.AddDotEnv(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.Configure<BroadcastOptions>(builder.Configuration.GetSection(BroadcastOptions.Section));
builder.Services.AddSingleton<ChannelDirectory>();
builder.Services.AddSingleton<LatencyStats>();
builder.Services.AddSingleton<LiveBroadcastService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LiveBroadcastService>());
builder.Services.AddSignalR();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<CommentaryHub>("/hubs/commentary");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
app.MapGet("/stats", (LatencyStats stats, ChannelDirectory directory) => Results.Ok(new { listeners = directory.ListenerCounts(), latency = stats.Snapshot() }));

app.Run();
