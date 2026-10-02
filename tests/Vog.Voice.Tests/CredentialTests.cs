using Azure.Core;

namespace Vog.Voice.Tests;

public sealed class CredentialTests
{
    private static readonly TokenRequestContext Speech = new(["https://cognitiveservices.azure.com/.default"]);

    private sealed class CountingCredential(TimeSpan lifetime) : TokenCredential
    {
        private int _calls;

        public int Calls => _calls;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var n = Interlocked.Increment(ref _calls);
            await Task.Delay(50, cancellationToken); // an az process takes seconds; any delay lets callers overlap
            return new AccessToken($"token-{n}", DateTimeOffset.UtcNow + lifetime);
        }
    }

    [Fact]
    public async Task ConcurrentCallersShareOneTokenRequest()
    {
        var inner = new CountingCredential(TimeSpan.FromHours(1));
        var credential = new CachingTokenCredential(inner);

        var tokens = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => credential.GetTokenAsync(Speech, TestContext.Current.CancellationToken).AsTask()));
        var later = credential.GetToken(Speech, TestContext.Current.CancellationToken);

        Assert.Equal(1, inner.Calls);
        Assert.All(tokens, t => Assert.Equal("token-1", t.Token));
        Assert.Equal("token-1", later.Token);
    }

    [Fact]
    public async Task TokensCloseToExpiryAreRefreshed()
    {
        var inner = new CountingCredential(TimeSpan.FromMinutes(2));
        var credential = new CachingTokenCredential(inner);

        await credential.GetTokenAsync(Speech, TestContext.Current.CancellationToken);
        var second = await credential.GetTokenAsync(Speech, TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Calls);
        Assert.Equal("token-2", second.Token);
    }

    [Fact]
    public async Task EachScopeHasItsOwnToken()
    {
        var inner = new CountingCredential(TimeSpan.FromHours(1));
        var credential = new CachingTokenCredential(inner);

        await credential.GetTokenAsync(Speech, TestContext.Current.CancellationToken);
        await credential.GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]), TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.Calls);
    }
}
