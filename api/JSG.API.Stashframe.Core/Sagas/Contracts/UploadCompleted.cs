using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Sagas.Contracts;

public record UploadCompleted
{
    public Guid MediaId { get; set; }
    public MediaCategory Category { get; set; }
}
