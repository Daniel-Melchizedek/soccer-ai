using System.Collections.Concurrent;
using Azure.Core;
using Azure.Identity;

namespace Vog.Voice;

/// <summary>
/// The one place credentials are chosen. There are no keys anywhere: the AI resource has key auth
/// disabled. In Azure the app uses its managed identity; on a developer machine, the Azure CLI
/// sign-in (az login). The Azure CLI is used explicitly rather than DefaultAzureCredential so a
/// Visual Studio sign-in to a different tenant can't be picked up by accident.
/// </summary>
public static class AzureCredentials
{
    private static readonly Lazy<TokenCredential> SharedCredential = new(() => new CachingTokenCredential(Create()));

    /// <summary>
    /// One credential for the whole process, caching tokens until shortly before they expire. Each
    /// Azure CLI token request starts an <c>az</c> process, which takes seconds, so channels share one.
    /// </summary>
    public static TokenCredential Shared => SharedCredential.Value;

    public static TokenCredential Create()
    {
        var managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        if (!string.IsNullOrEmpty(managedIdentityClientId) && Environment.GetEnvironmentVariable("IDENTITY_ENDPOINT") is not null)
        {
            return new ManagedIdentityCredential(ManagedIdentityId.FromUserAssignedClientId(managedIdentityClientId));
        }

        return new AzureCliCredential();
    }
}

/// <summary>Caches tokens per scope set, and makes concurrent callers share a single refresh.</summary>
public sealed class CachingTokenCredential(TokenCredential inner) : TokenCredential
{
    private static readonly TimeSpan RefreshBefore = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, AccessToken> _tokens = new();
    private readonly SemaphoreSlim _refresh = new(1, 1);

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        GetTokenAsync(requestContext, cancellationToken).AsTask().GetAwaiter().GetResult();

    public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var key = string.Join(' ', requestContext.Scopes) + "|" + requestContext.TenantId;
        if (Fresh(key) is { } cached)
        {
            return cached;
        }

        await _refresh.WaitAsync(cancellationToken);
        try
        {
            if (Fresh(key) is { } refreshed)
            {
                return refreshed;
            }

            var token = await inner.GetTokenAsync(requestContext, cancellationToken);
            _tokens[key] = token;
            return token;
        }
        finally
        {
            _refresh.Release();
        }
    }

    private AccessToken? Fresh(string key) =>
        _tokens.TryGetValue(key, out var token) && token.ExpiresOn - DateTimeOffset.UtcNow > RefreshBefore ? token : null;
}
