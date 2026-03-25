using JSG.API.Stashframe.Core.Enums;

namespace JSG.API.Stashframe.Core.Constants;

public static class SupportedMedia
{
    public static readonly IReadOnlyDictionary<string, MediaCategory> MimeTypes =
        new Dictionary<string, MediaCategory>
        {
            // Video
            ["video/mp4"] = MediaCategory.Video,
            ["video/webm"] = MediaCategory.Video,
            ["video/x-matroska"] = MediaCategory.Video,
            ["video/quicktime"] = MediaCategory.Video,
            ["video/x-msvideo"] = MediaCategory.Video,

            // Image
            ["image/png"] = MediaCategory.Screenshot,
            ["image/jpeg"] = MediaCategory.Screenshot,
            ["image/webp"] = MediaCategory.Screenshot,
            ["image/bmp"] = MediaCategory.Screenshot,
            ["image/tiff"] = MediaCategory.Screenshot,

            // Animated
            ["image/gif"] = MediaCategory.AnimatedImage,
            ["image/apng"] = MediaCategory.AnimatedImage,
        };

    private static readonly IReadOnlyDictionary<string, string> ExtensionMap =
        new Dictionary<string, string>
        {
            // Keyed extension map. This is currently used in the BlobPath raw format.
            ["video/mp4"] = "mp4",
            ["video/webm"] = "webm",
            ["video/x-matroska"] = "mkv",
            ["video/quicktime"] = "mov",
            ["video/x-msvideo"] = "avi",
            ["image/png"] = "png",
            ["image/jpeg"] = "jpg",
            ["image/webp"] = "webp",
            ["image/bmp"] = "bmp",
            ["image/tiff"] = "tif",
            ["image/gif"] = "gif",
            ["image/apng"] = "apng",
        };

    public static bool IsSupported(string mimeType)
        => MimeTypes.ContainsKey(mimeType.ToLowerInvariant());

    public static bool IsSupported(string mimeType, out MediaCategory category)
        => MimeTypes.TryGetValue(mimeType.ToLowerInvariant(), out category);

    public static MediaCategory? GetCategory(string mimeType)
        => MimeTypes.TryGetValue(mimeType.ToLowerInvariant(), out var cat) ? cat : null;

    public static string? GetExtension(string mimeType)
        => ExtensionMap.GetValueOrDefault(mimeType.ToLowerInvariant());
}
