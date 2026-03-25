namespace JSG.API.Stashframe.Core.Models;

public record UploadRequest
{
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long FileSize { get; init; }
}
