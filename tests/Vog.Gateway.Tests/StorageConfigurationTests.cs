using Microsoft.Extensions.Configuration;

namespace Vog.Gateway.Tests;

public sealed class StorageConfigurationTests
{
    [Fact]
    public void ReadsNormalizedEnvironmentVariable()
    {
        var prefix = $"VOG_TEST_{Guid.NewGuid():N}_";
        var name = prefix + "VogStorage__BlobEndpoint";
        const string endpoint = "https://example.blob.core.windows.net/";
        Environment.SetEnvironmentVariable(name, endpoint);
        try
        {
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables(prefix).Build();
            Assert.Null(configuration["VogStorage__BlobEndpoint"]);
            Assert.Equal(endpoint, StorageConfiguration.BlobEndpoint(configuration));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void NormalizedSettingTakesPrecedenceOverLocalLiteral()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VogStorage:BlobEndpoint"] = "https://azure.blob.core.windows.net/",
            ["VogStorage__BlobEndpoint"] = "https://local.blob.core.windows.net/",
        }).Build();
        Assert.Equal("https://azure.blob.core.windows.net/", StorageConfiguration.BlobEndpoint(configuration));
    }

    [Fact]
    public void ReadsLiteralLocalSetting()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VogStorage__BlobEndpoint"] = "https://local.blob.core.windows.net/",
        }).Build();
        Assert.Equal("https://local.blob.core.windows.net/", StorageConfiguration.BlobEndpoint(configuration));
    }
}