using JSG.API.Stashframe.Core.Database.Entities;
using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Interfaces.Repositories;

public interface IMediaStorageRepository
{
    Task<Media> CreateAsync(Media media);
    Task<Media?> GetByIdAsync(Guid id);
    Task<Media> ProcessedImageUpdateAsync(Guid mediaId, MediaStatus mediaStatus, int width, int height, long bytes);
    Task<bool> UpdateToProcessingAsync(Guid mediaId);
    Task<bool> UpdateToFailedAsync(Guid mediaId);
}