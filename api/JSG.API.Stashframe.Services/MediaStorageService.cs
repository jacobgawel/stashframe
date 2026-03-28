using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Humanizer;
using JSG.API.Stashframe.Core.Constants;
using JSG.API.Stashframe.Core.Database.Entities;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Interfaces.Repositories;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Models;
using Microsoft.Extensions.Logging;

namespace JSG.API.Stashframe.Services;

public class MediaStorageService(
    BlobServiceClient blobServiceClient,
    IMediaStorageRepository mediaStorageRepository,
    ILogger<MediaStorageService> logger) : IMediaStorageService
{
    async Task IMediaStorageService.UploadProcessedAsync(string blobContainer, string path, Stream stream,
        string contentType)
    {
        logger.LogInformation("Uploading processed blob to {Container}/{Path} ({ContentType})", blobContainer, path,
            contentType);

        var containerClient = blobServiceClient.GetBlobContainerClient(blobContainer);
        var blobClient = containerClient.GetBlobClient(path);

        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            }
        });

        logger.LogInformation("Processed blob uploaded to {Container}/{Path}", blobContainer, path);
    }

    async Task<ConfirmUploadResult> IMediaStorageService.ConfirmUploadAsync(Guid mediaId)
    {
        var media = await mediaStorageRepository.GetByIdAsync(mediaId);

        if (media is null)
            return new ConfirmUploadResult(ConfirmUploadStatus.MediaNotFound);

        if (media.MediaStatus is not MediaStatus.Pending)
            return new ConfirmUploadResult(ConfirmUploadStatus.AlreadyClaimed);

        var container = blobServiceClient.GetBlobContainerClient(BlobContainers.Raw);
        var exists = await container.GetBlobClient(media.RawBlobPath).ExistsAsync();

        if (!exists.Value)
            return new ConfirmUploadResult(ConfirmUploadStatus.BlobNotFound);

        var claimed = await mediaStorageRepository.UpdateToProcessingAsync(mediaId);

        if (!claimed)
        {
            return new ConfirmUploadResult(ConfirmUploadStatus.AlreadyClaimed);
        }

        return new ConfirmUploadResult(ConfirmUploadStatus.Success, media.Category);
    }

    async Task<bool> IMediaStorageService.UpdateToProcessingAsync(Guid mediaId)
    {
        return await mediaStorageRepository.UpdateToProcessingAsync(mediaId);
    }

    async Task IMediaStorageService.ProcessedImageUpdateAsync(Guid mediaId, MediaStatus mediaStatus, int width,
        int height, long bytes)
    {
        await mediaStorageRepository.ProcessedImageUpdateAsync(mediaId, mediaStatus, width, height, bytes);
    }

    public Task<bool> UpdateToFailedAsync(Guid mediaId)
    {
        return mediaStorageRepository.UpdateToFailedAsync(mediaId);
    }

    public Task<Media?> GetMediaAsync(Guid mediaId)
    {
        logger.LogDebug("Retrieving media {Id}", mediaId);
        return mediaStorageRepository.GetByIdAsync(mediaId);
    }

    async Task<Stream> IMediaStorageService.DownloadAsync(string blobContainer, string blobPath)
    {
        logger.LogInformation("Downloading blob from {Container}/{BlobPath}", blobContainer, blobPath);

        var containerClient = blobServiceClient.GetBlobContainerClient(blobContainer);
        var blobClient = containerClient.GetBlobClient(blobPath);

        var response = await blobClient.DownloadStreamingAsync();

        logger.LogInformation("Blob downloaded from {Container}/{BlobPath}", blobContainer, blobPath);

        return response.Value.Content;
    }

    async Task<SasUploadResult> IMediaStorageService.GenerateUploadSasAsync(string contentType, string fileName,
        long fileSize, TimeSpan expiry)
    {
        logger.LogInformation("Generating SAS upload URL for {FileName} ({ContentType}, {FileSize} bytes)", fileName,
            contentType, fileSize);

        var containerClient = blobServiceClient.GetBlobContainerClient(BlobContainers.Raw);

        var category = SupportedMedia.GetCategory(contentType);

        if (category is null)
        {
            logger.LogError("Unsupported content type {ContentType} for file {FileName}", contentType, fileName);
            throw new ArgumentException($"Content type '{contentType}' is not supported.");
        }

        var userId = Guid.NewGuid(); // TODO: replace with authenticated user ID
        var mediaId = Guid.NewGuid();

        var extension = SupportedMedia.GetExtension(contentType)!;
        var blobName = BlobPaths.RawOriginal(userId, mediaId, extension);

        var blobClient = containerClient.GetBlobClient(blobName);

        var sasExpiry = DateTimeOffset.UtcNow.Add(expiry);

        var userDelegationKey = await blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow, sasExpiry);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = BlobContainers.Raw,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = sasExpiry,
            ContentType = contentType
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, blobServiceClient.AccountName);
        var sasUri = new UriBuilder(blobClient.Uri) { Query = sasToken.ToString() }.Uri;

        await mediaStorageRepository.CreateAsync(new Media
        {
            Id = mediaId,
            UserId = userId,
            Category = category.Value,
            MediaStatus = MediaStatus.Pending,
            OriginalFilename = fileName,
            OriginalMimeType = contentType,
            OriginalSizeBytes = fileSize,
            RawBlobPath = blobName,
            CreatedAt = DateTime.UtcNow
        });

        logger.LogInformation(
            "Media record created and SAS URL generated for {MediaId} (user {UserId}, category {Category}, blob {BlobName})",
            mediaId, userId, category.Value, blobName);

        return new SasUploadResult
        {
            MediaId = mediaId,
            UserId = userId,
            UploadUrl = sasUri.ToString(),
            BlobName = blobName,
            ContainerName = BlobContainers.Raw,
            Category = category.Value.ToString(),
            ExpiresIn = expiry.Humanize()
        };
    }
}