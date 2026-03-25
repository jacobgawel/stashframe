using JSG.API.Stashframe.Core.Database;
using JSG.API.Stashframe.Core.Database.Entities;
using JSG.API.Stashframe.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JSG.API.Stashframe.Repositories;

public class MediaStorageRepository(StashframeContext context, ILogger<MediaStorageRepository> logger) : IMediaStorageRepository
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
}
