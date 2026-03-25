using Azure.Storage.Blobs;
using JSG.API.Stashframe.Core.Constants;

namespace JSG.API.Stashframe.Extensions;

public static class BlobStorageExtensions
{
    private static readonly string[] Containers = [BlobContainers.Raw, BlobContainers.Transcoded, BlobContainers.Thumbnails, BlobContainers.Screenshots];

    public static async Task EnsureBlobContainersAsync(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<BlobServiceClient>>();
        var blobServiceClient = app.Services.GetRequiredService<BlobServiceClient>();

        foreach (var container in Containers)
        {
            logger.LogInformation("Ensuring blob container {ContainerName} exists", container);
            await blobServiceClient.GetBlobContainerClient(container).CreateIfNotExistsAsync();
        }

        logger.LogInformation("All blob containers verified");
    }
}
