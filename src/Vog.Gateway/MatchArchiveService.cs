using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vog.Voice;

namespace Vog.Gateway;

/// <summary>
/// Archives a lightweight match summary to Azure Blob Storage after each completed match.
/// Exposes the last N summaries for the /history endpoint and the UI's recent-matches list.
///
/// Graceful degradation: if the storage endpoint is not configured (local dev without Azure),
/// every operation is a no-op so the live commentary is never affected.
/// </summary>
public sealed class MatchArchiveService(IConfiguration config, ILogger<MatchArchiveService> log)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly BlobContainerClient? _container = TryCreateContainer(config, log);

    private static BlobContainerClient? TryCreateContainer(IConfiguration config, ILogger log)
    {
        var endpoint = config["VogStorage__BlobEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            log.LogInformation("VogStorage__BlobEndpoint not configured – match archiving disabled");
            return null;
        }

        try
        {
            var containerUri = new Uri(endpoint.TrimEnd('/') + "/vog-data");
            Azure.Core.TokenCredential cred = AzureCredentials.Shared;
            return new BlobContainerClient(containerUri, cred);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not create blob container client – match archiving disabled");
            return null;
        }
    }

    /// <summary>Uploads a match summary blob. Never throws; failures are logged and silently skipped.</summary>
    public async Task ArchiveAsync(MatchSummary summary, CancellationToken ct = default)
    {
        if (_container is null) return;
        try
        {
            var name = $"match-snapshots/{summary.Seed}/{summary.UtcDate:yyyyMMddHHmmss}.json";
            var json = JsonSerializer.Serialize(summary, JsonOpts);
            await _container.GetBlobClient(name).UploadAsync(BinaryData.FromString(json), overwrite: true, ct);
            log.LogInformation("Archived match seed={Seed} score={H}-{A} to {Name}", summary.Seed, summary.HomeScore, summary.AwayScore, name);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to archive match seed={Seed} – live commentary unaffected", summary.Seed);
        }
    }

    /// <summary>Returns the most recent summaries, ordered newest-first. Returns empty list on any error.</summary>
    public async Task<IReadOnlyList<MatchSummary>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        if (_container is null) return [];
        try
        {
            var results = new List<(DateTimeOffset Date, MatchSummary Summary)>();
            await foreach (var blob in _container.GetBlobsAsync(prefix: "match-snapshots/", traits: BlobTraits.None, cancellationToken: ct))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var blobClient = _container.GetBlobClient(blob.Name);
                    var content = await blobClient.DownloadContentAsync(ct);
                    var summary = JsonSerializer.Deserialize<MatchSummary>(content.Value.Content.ToString(), JsonOpts);
                    if (summary is not null)
                        results.Add((blob.Properties.LastModified ?? DateTimeOffset.MinValue, summary));
                }
                catch
                {
                    // Skip malformed blobs.
                }
            }

            return results.OrderByDescending(r => r.Date).Take(count).Select(r => r.Summary).ToList();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to retrieve match history");
            return [];
        }
    }
}

/// <summary>Lightweight match summary stored after each completed match.</summary>
public sealed record MatchSummary(
    ulong Seed,
    DateTimeOffset UtcDate,
    int HomeScore,
    int AwayScore,
    string HomeName,
    string AwayName,
    IReadOnlyList<GoalRecord> Goals,
    long MatchLengthMs);

/// <summary>Goal record within a match summary.</summary>
public sealed record GoalRecord(int Minute, string Team, string PlayerName);
