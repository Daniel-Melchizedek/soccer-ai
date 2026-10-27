using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Vog.Gateway.Tests;

public sealed class HistoricalDataTests
{
    [Theory]
    [InlineData("results.csv")]
    [InlineData("../kaggle/results.csv")]
    [InlineData("GOALS-PER-DECADE.JSON")]
    public async Task RejectsUnlistedFilesBeforeReadingStorage(string file)
    {
        var data = new FakeData();
        var result = await Get(file, data, cancellationToken: TestContext.Current.CancellationToken);
        Assert.IsType<NotFound>(result);
        Assert.Null(data.File);
    }

    [Theory]
    [InlineData("goals-per-decade.json")]
    [InlineData("home-advantage.json")]
    public async Task ReturnsOnlyRequestedAggregateAndForwardsCancellation(string file)
    {
        using var cancellation = new CancellationTokenSource();
        var data = new FakeData { Json = "[{\"decade\":\"2020s\"}]" };
        var context = new DefaultHttpContext();
        var result = Assert.IsType<ContentHttpResult>(await Get(file, data, context, cancellation.Token));
        Assert.Equal(data.Json, result.ResponseContent);
        Assert.StartsWith("application/json", result.ContentType);
        Assert.Equal(file, data.File);
        Assert.Equal(cancellation.Token, data.Token);
        Assert.Equal("public, max-age=3600", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task MissingDataReturnsNotFound()
    {
        Assert.IsType<NotFound>(await Get("goals-per-decade.json", new FakeData(), cancellationToken: TestContext.Current.CancellationToken));
        var storage = new HistoricalData(new ConfigurationBuilder().Build());
        Assert.Null(await storage.ReadAsync("goals-per-decade.json", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StorageFailureIsUnavailableWithoutLeakingDetails()
    {
        var context = new DefaultHttpContext();
        var result = Assert.IsType<ProblemHttpResult>(await Get("home-advantage.json",
            new FakeData { Error = new InvalidOperationException("private details") }, context, TestContext.Current.CancellationToken));
        Assert.Equal(503, result.StatusCode);
        Assert.Null(result.ProblemDetails.Detail);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task CancellationIsNotConvertedToStorageFailure()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() => Get("home-advantage.json",
            new FakeData { Error = new OperationCanceledException() }, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static async Task<IResult> Get(string file, FakeData data, HttpContext? context = null,
        CancellationToken cancellationToken = default) =>
        (await HistoricalDataEndpoints.GetAsync(file, data, NullLogger<HistoricalData>.Instance,
            context ?? new DefaultHttpContext(), cancellationToken)).Result;

    private sealed class FakeData : IHistoricalData
    {
        public string? Json { get; init; }
        public Exception? Error { get; init; }
        public string? File { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<string?> ReadAsync(string file, CancellationToken cancellationToken)
        {
            File = file;
            Token = cancellationToken;
            return Error is null ? Task.FromResult(Json) : Task.FromException<string?>(Error);
        }
    }
}