using JSG.API.Stashframe.Core.Database.Entities;

namespace JSG.API.Stashframe.Core.Interfaces.Repositories;

public interface IMediaStorageRepository
{
    Task<Media> CreateAsync(Media media);
    Task<Media?> GetByIdAsync(Guid id);
}
