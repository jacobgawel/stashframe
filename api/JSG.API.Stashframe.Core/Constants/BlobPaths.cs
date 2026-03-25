namespace JSG.API.Stashframe.Core.Constants;

public static class BlobPaths
{
    // Raw uploads: {userId}/{mediaId}/original.{ext}
    public static string RawOriginal(Guid userId, Guid mediaId, string extension) => $"{userId}/{mediaId}/original.{extension}";
    public static string RawMetadata(Guid userId, Guid mediaId) => $"{userId}/{mediaId}/metadata.json";

    // Transcoded: {mediaId}/master.m3u8, {mediaId}/{profile}/playlist.m3u8

    // Thumbnails: {mediaId}/primary_sm.webp, etc.

    // Screenshots: {mediaId}/full.webp, thumb_sm.webp, etc.
    public static string ScreenshotFull(Guid mediaId, string format = "webp") => $"{mediaId}/full.{format}";
    public static string ScreenshotThumb(Guid mediaId, string variant) => $"{mediaId}/thumb_{variant}.webp";

    // Avatars: {userId}/avatar_sm.webp, etc.
    public static string Avatar(Guid userId, string variant) => $"{userId}/avatar_{variant}.webp";
    public static string Banner(Guid userId) => $"{userId}/banner.webp";

    // Exports: {userId}/{exportId}.zip
    public static string Export(Guid userId, Guid exportId) => $"{userId}/{exportId}.zip";
}
