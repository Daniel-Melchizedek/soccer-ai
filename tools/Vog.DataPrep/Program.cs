// Vog.DataPrep: downloads results.csv from Azure Blob Storage, aggregates into two JSON files,
// and uploads the results back to blob storage for the gateway's /data/ endpoint.
//
// Data: Mart Jürisoo, "International football results from 1872 to 2017", Kaggle, CC0 Public Domain.
// https://www.kaggle.com/datasets/martj42/international-football-results-from-1872-to-2017
//
// Usage:
//   dotnet run --project tools/Vog.DataPrep -- --storage https://<account>.blob.core.windows.net
//
// Prerequisites:
//   - Upload results.csv to blob container vog-data/kaggle/results.csv
//   - Run 'az login' or use managed identity
//   - The identity needs Storage Blob Data Contributor on the storage account

using Azure.Identity;
using Azure.Storage.Blobs;
using System.Globalization;
using System.Text.Json;

var endpoint = args.SkipWhile(a => a != "--storage").Skip(1).FirstOrDefault()
    ?? Environment.GetEnvironmentVariable("VogStorage__BlobEndpoint")
    ?? throw new InvalidOperationException("Provide --storage <endpoint> or set VogStorage__BlobEndpoint");

Console.WriteLine($"Storage: {endpoint}");

var credential = new DefaultAzureCredential();
var serviceClient = new BlobServiceClient(new Uri(endpoint), credential);
var container = serviceClient.GetBlobContainerClient("vog-data");

// Ensure public container exists (already created by Bicep in production).
await container.CreateIfNotExistsAsync();

Console.WriteLine("Downloading results.csv from blob storage...");
var csvBlob = container.GetBlobClient("kaggle/results.csv");

if (!await csvBlob.ExistsAsync())
{
    Console.Error.WriteLine(
        "ERROR: kaggle/results.csv not found in the vog-data container.\n" +
        "Upload it first:\n" +
        "  az storage blob upload --account-name <acct> --container-name vog-data " +
        "--name kaggle/results.csv --file results.csv --auth-mode login");
    return 1;
}

using var csvStream = await csvBlob.OpenReadAsync();
using var reader = new StreamReader(csvStream);

// Aggregate: goals per decade and home-win % per decade.
var goalsByDecade = new Dictionary<int, (int Total, int Count)>();
var homeWinsByDecade = new Dictionary<int, (int HomeWins, int Total)>();

await reader.ReadLineAsync(); // skip header row

string? line;
int rows = 0, skipped = 0;
while ((line = await reader.ReadLineAsync()) is not null)
{
    var parts = line.Split(',');
    if (parts.Length < 6)
    {
        skipped++;
        continue;
    }

    // CSV columns: date, home_team, away_team, home_score, away_score, tournament, city, country, neutral
    if (!DateTime.TryParse(parts[0].Trim('"'), CultureInfo.InvariantCulture, out var date) ||
        !int.TryParse(parts[3].Trim('"'), out var homeScore) ||
        !int.TryParse(parts[4].Trim('"'), out var awayScore))
    {
        skipped++;
        continue;
    }

    var decade = (date.Year / 10) * 10;
    var totalGoals = homeScore + awayScore;

    var (t, c) = goalsByDecade.GetValueOrDefault(decade);
    goalsByDecade[decade] = (t + totalGoals, c + 1);

    var (hw, ht) = homeWinsByDecade.GetValueOrDefault(decade);
    homeWinsByDecade[decade] = (hw + (homeScore > awayScore ? 1 : 0), ht + 1);
    rows++;
}

Console.WriteLine($"Processed {rows} rows, skipped {skipped}.");

if (rows == 0)
{
    Console.Error.WriteLine("ERROR: No rows were parsed. Check the CSV format.");
    return 1;
}

// Build output JSON.
var goalsJson = goalsByDecade
    .OrderBy(kv => kv.Key)
    .Select(kv => new { decade = $"{kv.Key}s", avgGoals = Math.Round((double)kv.Value.Total / kv.Value.Count, 2) })
    .ToList();

var homeJson = homeWinsByDecade
    .OrderBy(kv => kv.Key)
    .Select(kv => new { decade = $"{kv.Key}s", homeWinPct = Math.Round((double)kv.Value.HomeWins / kv.Value.Total * 100, 1) })
    .ToList();

var opts = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
var goalsBytes = JsonSerializer.SerializeToUtf8Bytes(goalsJson, opts);
var homeBytes = JsonSerializer.SerializeToUtf8Bytes(homeJson, opts);

Console.WriteLine("Uploading aggregated JSON to blob storage...");
await container.GetBlobClient("public/goals-per-decade.json")
    .UploadAsync(new BinaryData(goalsBytes), overwrite: true);
await container.GetBlobClient("public/home-advantage.json")
    .UploadAsync(new BinaryData(homeBytes), overwrite: true);

Console.WriteLine($"Done. Uploaded {goalsJson.Count} decades to public/goals-per-decade.json");
Console.WriteLine($"Done. Uploaded {homeJson.Count} decades to public/home-advantage.json");
return 0;
