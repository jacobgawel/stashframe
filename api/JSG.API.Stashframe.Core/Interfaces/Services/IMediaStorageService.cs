using JSG.API.Stashframe.Core.Database.Entities;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Models;

namespace JSG.API.Stashframe.Core.Interfaces.Services;

public interface IMediaStorageService
{
    Task<Media?> GetMediaAsync(Guid mediaId);
    Task<Stream> DownloadAsync(string blobContainer, string blobPath);
    Task<SasUploadResult> GenerateUploadSasAsync(string contentType, string fileName, long fileSize, TimeSpan expiry);
    Task UploadProcessedAsync(string blobContainer, string path, Stream stream, string contentType);
    Task<ConfirmUploadResult> ConfirmUploadAsync(Guid mediaId);
    Task<bool> UpdateToProcessingAsync(Guid mediaId);
    Task ProcessedImageUpdateAsync(Guid mediaId, MediaStatus mediaStatus, int width, int height, long bytes);
    Task<bool> UpdateToFailedAsync(Guid mediaId);
}
