using System.ComponentModel;

namespace JSG.API.Stashframe.Core.Models;

public class SasUploadResult
{
    public required Guid MediaId { get; set; }
    public required Guid UserId { get; set; }
    public required string UploadUrl { get; set; }
    public required string BlobName { get; set; }
    public required string ContainerName { get; set; }
    public required string Category { get; set; }
    public required string ExpiresIn { get; set; }
}
