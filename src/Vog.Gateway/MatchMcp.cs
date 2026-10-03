using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using Vog.Companion;

namespace Vog.Gateway;

/// <summary>The shared secret Foundry presents to the MCP endpoint, kept in Key Vault as Mcp--ApiKey.</summary>
public sealed class McpOptions
{
    public const string Section = "Mcp";

    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// The live match as an MCP server, for the Foundry agents (BUILD_PLAN.md: one MatchQuery service).
/// Each language has its own path, /mcp/{lang}, so places and team names come back in the words of
/// that language's commentary; the agent for that language is registered against that path. The
/// endpoint is public, because Foundry Agent Service calls it from the internet, and every request
/// must carry the X-Api-Key held in the Foundry project connection.
/// </summary>
public static class MatchMcp
{
    public const string Path = "/mcp";

    public static IServiceCollection AddMatchMcp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpOptions>()
            .Bind(configuration.GetSection(McpOptions.Section))
            .Validate(o => o.ApiKey.Length >= 32, "Mcp:ApiKey must be set (at least 32 characters): infra/provision.ps1 stores it in Key Vault as Mcp--ApiKey.")
            .ValidateOnStart();

        services.AddHttpContextAccessor();
        services.AddSingleton<ConcurrentDictionary<string, MatchTools>>();
        services.AddMcpServer(o => o.ServerInfo = new Implementation { Name = "vog-match", Version = "1.0.0" })
            .WithHttpTransport(o => o.Stateless = true)
            .WithListToolsHandler((_, _) => ValueTask.FromResult(new ListToolsResult { Tools = Tools }))
            .WithCallToolHandler((request, ct) => ValueTask.FromResult(Call(request.Services!, request.Params!)));
        return services;
    }

    public static WebApplication MapMatchMcp(this WebApplication app)
    {
        var expected = Encoding.UTF8.GetBytes(app.Services.GetRequiredService<IOptions<McpOptions>>().Value.ApiKey);
        var log = app.Services.GetRequiredService<ILogger<McpOptions>>();

        app.UseWhen(c => c.Request.Path.StartsWithSegments(Path), branch => branch.Use(async (context, next) =>
        {
            // Fixed-time comparison, so the check can't leak the key a byte at a time.
            var provided = Encoding.UTF8.GetBytes(context.Request.Headers["X-Api-Key"].ToString());
            if (!CryptographicOperations.FixedTimeEquals(provided, expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
            else
            {
                await next();
            }

            // A refused request from Foundry is otherwise invisible: say what it asked for (never the key).
            if (context.Response.StatusCode >= 400)
            {
                var headers = context.Request.Headers;
                log.LogWarning(
                    "MCP {Method} {Path} -> {Status} (Accept: {Accept}; Content-Type: {ContentType}; session id: {HasSession}; protocol: {Protocol}; agent: {UserAgent})",
                    context.Request.Method, context.Request.Path, context.Response.StatusCode, headers.Accept.ToString(), context.Request.ContentType,
                    headers.ContainsKey("Mcp-Session-Id"), headers["Mcp-Protocol-Version"].ToString(), headers.UserAgent.ToString());
            }
        }));

        app.MapMcp($"{Path}/{{lang}}");
        return app;
    }

    private static readonly IList<Tool> Tools = MatchTools.Specs
        .Select(s => new Tool { Name = s.Name, Description = s.Description, InputSchema = JsonDocument.Parse(s.ParametersSchema).RootElement.Clone() })
        .ToList();

    private static CallToolResult Call(IServiceProvider services, CallToolRequestParams call)
    {
        var started = Stopwatch.GetTimestamp();
        var http = services.GetRequiredService<IHttpContextAccessor>().HttpContext;
        var lang = http?.GetRouteValue("lang") as string ?? string.Empty;
        var directory = services.GetRequiredService<ChannelDirectory>();
        var log = services.GetRequiredService<ILogger<MatchTools>>();

        if (!directory.Available.Any(c => c.Lang.Equals(lang, StringComparison.OrdinalIgnoreCase)))
        {
            return Result($$"""{"error":"This server has no commentary language '{{lang}}'."}""");
        }

        var broadcast = services.GetRequiredService<LiveBroadcastService>();
        var tools = services.GetRequiredService<ConcurrentDictionary<string, MatchTools>>()
            .GetOrAdd(lang, l => new MatchTools(() => broadcast.Snapshot, l));
        var arguments = call.Arguments is null ? null : JsonSerializer.Serialize(call.Arguments);
        var output = tools.Invoke(call.Name, arguments);

        services.GetRequiredService<LatencyStats>().RecordToolCall();
        log.LogInformation("MCP {Tool} ({Lang}) answered in {Ms:0.0} ms", call.Name, lang, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        return Result(output);
    }

    private static CallToolResult Result(string json) => new()
    {
        Content = [new TextContentBlock { Text = json }],
        IsError = json.StartsWith("{\"error\"", StringComparison.Ordinal),
    };
}
