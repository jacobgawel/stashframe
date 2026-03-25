using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Sagas.Contracts;

public class UploadCompleted
{
    public Guid MediaId { get; set; }
    public MediaCategory Category { get; set; }
}
