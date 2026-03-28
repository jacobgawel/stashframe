using JSG.API.Stashframe.Core.Database;
using JSG.API.Stashframe.Core.Database.Entities;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JSG.API.Stashframe.Repositories;

public class MediaStorageRepository(StashframeContext context, ILogger<MediaStorageRepository> logger)
    : IMediaStorageRepository
{
    public async Task<Media> CreateAsync(Media media)
    {
        logger.LogDebug("Creating media record {MediaId} for user {UserId}", media.Id, media.UserId);

        context.Media.Add(media);
        await context.SaveChangesAsync();

        logger.LogDebug("Media record {MediaId} persisted", media.Id);

        return media;
    }

    public async Task<Media?> GetByIdAsync(Guid id)
    {
        logger.LogDebug("Fetching media record {MediaId}", id);

        var media = await context.Media.FirstOrDefaultAsync(m => m.Id == id);

        if (media is null)
        {
            logger.LogDebug("Media record {MediaId} not found", id);
        }

        return media;
    }

    public async Task<Media> ProcessedImageUpdateAsync(Guid mediaId, MediaStatus mediaStatus, int width, int height, long bytes)
    {
        var media = await context.Media.FindAsync(mediaId);

        if (media is null)
        {
            throw new KeyNotFoundException(
                $"Media not found for MediaID: {mediaId}. Could not update processed image.");
        }

        media.MediaStatus = mediaStatus;
        media.Height = height;
        media.Width = width;
        media.OriginalSizeBytes = bytes;

        await context.SaveChangesAsync();
        
        logger.LogDebug("Updated media record to ready for media record {MediaId}", mediaId);

        return media;
    }

    public async Task<bool> UpdateToProcessingAsync(Guid mediaId)
    {
        var rows = await context.Media
            .Where(m => m.Id == mediaId && m.MediaStatus == MediaStatus.Pending)
            .ExecuteUpdateAsync(s => 
                s.SetProperty(m => m.MediaStatus, MediaStatus.Processing));

        return rows > 0;
    }
    
    public async Task<bool> UpdateToFailedAsync(Guid mediaId)
    {
        var rows = await context.Media
            .Where(m => m.Id == mediaId && m.MediaStatus == MediaStatus.Processing)
            .ExecuteUpdateAsync(s => 
                s.SetProperty(m => m.MediaStatus, MediaStatus.Failed));

        return rows > 0;
    }
}