using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Database.Entities;

public class Media
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public MediaCategory Category { get; set; }
    public MediaStatus MediaStatus { get; set; }
    public required string OriginalFilename { get; set; }
    public required string OriginalMimeType { get; set; }
    public long OriginalSizeBytes { get; set; }
    public required string RawBlobPath { get; set; }

    // metadata
    public int Width { get; set; }
    public int Height { get; set; }
    public int DurationSeconds { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime DeletedAt { get; set; }

    public ICollection<ShareLink> ShareLinks { get; set; } = [];
}
