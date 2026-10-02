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
