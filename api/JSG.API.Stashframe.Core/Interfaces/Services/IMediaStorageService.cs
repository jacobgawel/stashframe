using JSG.API.Stashframe.Core.Models;

namespace JSG.API.Stashframe.Core.Interfaces.Services;

public interface IMediaStorageService
{
    Task<Stream> DownloadAsync(string blobContainer, string blobPath);
    Task<SasUploadResult> GenerateUploadSasAsync(string contentType, string fileName, long fileSize, TimeSpan expiry);
    Task UploadProcessedAsync(string blobContainer, string path, Stream stream, string contentType);
}
