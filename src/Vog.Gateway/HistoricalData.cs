using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http.HttpResults;
using Vog.Voice;

namespace Vog.Gateway;

public interface IHistoricalData
{
    Task<string?> ReadAsync(string file, CancellationToken cancellationToken);
}

public sealed class HistoricalData(IConfiguration configuration) : IHistoricalData
{
    public async Task<string?> ReadAsync(string file, CancellationToken cancellationToken)
    {
        if (!HistoricalDataEndpoints.IsAllowed(file)) return null;
        var endpoint = StorageConfiguration.BlobEndpoint(configuration);
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        var blob = new BlobClient(new Uri($"{endpoint.TrimEnd('/')}/vog-data/public/{file}"), AzureCredentials.Shared);
        try
        {
            var response = await blob.DownloadContentAsync(cancellationToken);
            return response.Value.Content.ToString();
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return null;
        }
    }
}

public static class HistoricalDataEndpoints
{
    public static bool IsAllowed(string file) => file is "goals-per-decade.json" or "home-advantage.json";

    public static void MapHistoricalData(this WebApplication app) =>
        app.MapGet("/data/{file}", GetAsync)
            .WithName("GetHistoricalFootballData")
            .WithSummary("Read one of two historical international football aggregates.");

    public static async Task<Results<ContentHttpResult, NotFound, ProblemHttpResult>> GetAsync(
        string file, IHistoricalData data, ILogger<HistoricalData> logger, HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!IsAllowed(file)) return TypedResults.NotFound();
        try
        {
            var json = await data.ReadAsync(file, cancellationToken);
            if (json is null) return TypedResults.NotFound();
            context.Response.Headers.CacheControl = "public, max-age=3600";
            return TypedResults.Content(json, "application/json");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Could not read historical aggregate {File}", file);
            return TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Historical data is temporarily unavailable.");
        }
    }
}