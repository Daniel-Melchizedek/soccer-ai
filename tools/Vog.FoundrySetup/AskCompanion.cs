#pragma warning disable OPENAI001
using System.Diagnostics;
using Azure.AI.Projects;
using OpenAI.Responses;

/// <summary>
/// Asks a deployed vog-match-companion-&lt;lang&gt; agent one question through the Foundry Responses
/// API. Foundry Agent Service runs its MCP tools against the deployed gateway's live match, so this
/// checks the agent, the connection and the endpoint together, without Voice Live.
/// </summary>
internal static class AskCompanion
{
    public static async Task<int> RunAsync(AIProjectClient client, string lang, string question)
    {
        var agent = $"vog-match-companion-{lang.ToLowerInvariant()}";
        var responses = client.ProjectOpenAIClient.GetProjectResponsesClientForAgent(agent);

        // Sign in first, so the timing below is the agent's, not the Azure CLI's.
        await Vog.Voice.AzureCredentials.Shared.GetTokenAsync(new Azure.Core.TokenRequestContext(["https://ai.azure.com/.default"]), default);

        var options = new CreateResponseOptions();
        options.InputItems.Add(ResponseItem.CreateUserMessageItem(question));
        var started = Stopwatch.GetTimestamp();
        ResponseResult response = await responses.CreateResponseAsync(options);

        foreach (var item in response.OutputItems)
        {
            switch (item)
            {
                case McpToolCallApprovalRequestItem approval:
                    throw new InvalidOperationException($"Foundry asked for approval to run '{approval.ToolName}': re-register the agents (approval must be 'never').");
                case McpToolCallItem call:
                    Console.WriteLine($"  tool {call.ToolName}({call.ToolArguments}) {(call.Error is null ? "ok" : $"failed: {call.Error}")}");
                    break;
            }
        }

        Console.WriteLine(agent);
        Console.WriteLine($"Q: {question}");
        Console.WriteLine($"A: {response.GetOutputText()}");
        Console.WriteLine($"   {Stopwatch.GetElapsedTime(started).TotalMilliseconds:0} ms · tokens in {response.Usage?.InputTokenCount} out {response.Usage?.OutputTokenCount}");
        return 0;
    }
}
