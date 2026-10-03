#pragma warning disable OPENAI001 // Foundry agent definitions use the OpenAI Responses tool types.
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using OpenAI.Responses;
using Vog.Companion;
using Vog.Replay;
using Vog.Voice;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

// Registers every agent in foundry/agents/*.agent.yaml as a new version of a Foundry prompt agent
// (the certflow-ai RegisterAgentAsync pattern), so prompts are reviewed in pull requests and each
// deployment is a visible, immutable version in the Foundry portal. An agent with languages is
// registered once per language. Its tools are served by the gateway's MCP endpoint; Foundry Agent
// Service calls them with the key held in the project connection.

DotEnv.Load();
var projectEndpoint = Required("VOG_FOUNDRY_PROJECT_ENDPOINT");
var model = Required("VOG_AGENT_MODEL_DEPLOYMENT");
var guardrailId = Required("VOG_AGENT_GUARDRAIL_ID");
var mcpUrl = Required("VOG_MCP_URL").TrimEnd('/');
var mcpConnectionPrefix = Required("VOG_MCP_CONNECTION_PREFIX");

var client = new AIProjectClient(new Uri(projectEndpoint), AzureCredentials.Shared);

// --ask <lang> "question": asks the deployed companion one question, to check it end to end.
if (args is ["--ask", var askLang, var question, ..])
{
    return await AskCompanion.RunAsync(client, askLang, question);
}

// --delete <agent>: removes an agent, with all its versions, that no definition registers any more.
if (args is ["--delete", var retired, ..])
{
    await client.AgentAdministrationClient.DeleteAgentAsync(retired);
    Console.WriteLine($"Deleted {retired} and all its versions");
    return 0;
}

var agentsDir = FindAgentsDirectory();
var files = Directory.GetFiles(agentsDir, "*.agent.yaml").Order(StringComparer.Ordinal).ToList();
if (files.Count == 0)
{
    Console.Error.WriteLine($"No *.agent.yaml files in {agentsDir}.");
    return 1;
}

var yaml = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
var known = MatchTools.Specs.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

foreach (var file in files)
{
    var spec = yaml.Deserialize<AgentSpec>(File.ReadAllText(file));
    if (spec.Tools.FirstOrDefault(t => !known.Contains(t)) is { } unknown)
    {
        throw new InvalidOperationException($"{Path.GetFileName(file)} names tool '{unknown}', which the gateway's MCP endpoint doesn't serve.");
    }

    foreach (var (lang, language) in spec.Languages)
    {
        var name = $"{spec.Name}-{lang.ToLowerInvariant()}";
        var definition = new DeclarativeAgentDefinition(model)
        {
            Instructions = spec.Instructions.Replace("{language}", language, StringComparison.Ordinal),
            // The full ARM id: Foundry accepts a bare policy name without error, then filters nothing.
            ContentFilterConfiguration = new ContentFilterConfiguration(guardrailId),
        };

        if (spec.ReasoningEffort is { Length: > 0 } effort)
        {
            definition.ReasoningOptions = new ResponseReasoningOptions { ReasoningEffortLevel = new ResponseReasoningEffortLevel(effort) };
        }

        if (spec.Tools.Count > 0)
        {
            definition.Tools.Add(MatchMcpTool($"{mcpUrl}/{lang}", $"{mcpConnectionPrefix}-{lang.ToLowerInvariant()}", spec.Tools));
        }

        ProjectsAgentVersion version = await client.AgentAdministrationClient.CreateAgentVersionAsync(
            agentName: name,
            options: new(definition) { Description = spec.Description },
            foundryFeatures: null);
        Console.WriteLine($"Registered {name} version {version.Version} ({model}, reasoning {spec.ReasoningEffort ?? "default"}, {spec.Tools.Count} MCP tools, guardrail attached)");
    }
}

return 0;

// One MCP tool, scoped by an allow-list to the tools this agent may call. Nobody is watching to
// approve tool calls mid-question, and they are all read-only, so approval is switched off: the
// Foundry default ("always") would stall every answer. The key lives in the project connection, and
// Foundry sends the requests to that connection's target, so each language has its own connection.
ResponseTool MatchMcpTool(string serverUrl, string connection, IEnumerable<string> allowed)
{
    var filter = new McpToolFilter();
    foreach (var tool in allowed)
    {
        filter.ToolNames.Add(tool);
    }

    var mcp = (McpTool)ResponseTool.CreateMcpTool(
        serverLabel: "vog_match",
        serverUri: new Uri(serverUrl),
        allowedTools: filter,
        toolCallApprovalPolicy: new McpToolCallApprovalPolicy(GlobalMcpToolCallApprovalPolicy.NeverRequireApproval));
    mcp.ProjectConnectionId = connection;
    return mcp;
}

static string Required(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"{name} is not set. Run infra/provision.ps1, which writes it to .env.local.");

static string FindAgentsDirectory()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, "foundry", "agents");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
    }

    throw new DirectoryNotFoundException("foundry/agents not found above the current directory.");
}

internal sealed class AgentSpec
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Language tag to language name, substituted for {language} in the instructions.</summary>
    public Dictionary<string, string> Languages { get; set; } = [];

    /// <summary>For gpt-5.6-terra: none, low, medium, high, xhigh or max; the model's default when left out.</summary>
    public string? ReasoningEffort { get; set; }

    public List<string> Tools { get; set; } = [];

    public string Instructions { get; set; } = string.Empty;
}
