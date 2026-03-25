using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Database.Entities;

public class ShareLink
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid UserId { get; set; }
    public required string Slug { get; set; }
    public ShareVisibility Visibility { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public long ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public Media Media { get; set; } = null!;
}
