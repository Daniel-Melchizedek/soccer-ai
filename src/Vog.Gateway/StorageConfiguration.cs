namespace Vog.Gateway;

public static class StorageConfiguration
{
    public static string? BlobEndpoint(IConfiguration configuration) =>
        configuration["VogStorage:BlobEndpoint"] ?? configuration["VogStorage__BlobEndpoint"];
}