using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JSG.API.Stashframe.Core.Constants;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Sagas.MediaProcessing.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;

namespace JSG.API.Stashframe.Services.Consumers;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProcessImageConsumer(
    ILogger<ProcessImageConsumer> logger,
    IPublishEndpoint publishEndpoint,
    IMediaStorageService storage,
    IImageProcessingService imageProcessor) : IConsumer<ProcessImage>
{
    public async Task Consume(ConsumeContext<ProcessImage> context)
    {
        var message = context.Message;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Processing image for {MediaId} (CorrelationId: {CorrelationId})", message.MediaId,
            message.CorrelationId);

        var media = await storage.GetMediaAsync(message.MediaId);

        if (media is null)
        {
            logger.LogWarning("Media {MediaId} not found in database — skipping processing", message.MediaId);
            return;
        }

        var lap = Stopwatch.StartNew();

        logger.LogInformation("Downloading raw blob for {MediaId} from {BlobPath}", media.Id, media.RawBlobPath);
        await using var rawStream = await storage.DownloadAsync(BlobContainers.Raw, media.RawBlobPath);
        logger.LogDebug("Download completed for {MediaId} in {ElapsedMs}ms", media.Id, lap.ElapsedMilliseconds);

        lap.Restart();
        using var image = await Image.LoadAsync(rawStream);
        logger.LogDebug("Image decoded for {MediaId} in {ElapsedMs}ms — {Width}x{Height}", media.Id, lap.ElapsedMilliseconds, image.Width, image.Height);

        lap.Restart();
        var fullWebp = await imageProcessor.OptimiseAsync(image, OutputFormat.WebP);
        logger.LogDebug("Full WebP encode for {MediaId} in {ElapsedMs}ms", media.Id, lap.ElapsedMilliseconds);

        lap.Restart();
        var fullPath = BlobPaths.ScreenshotFull(message.MediaId);
        await storage.UploadProcessedAsync(BlobContainers.Screenshots, fullPath, fullWebp, "image/webp");
        logger.LogDebug("Full WebP upload for {MediaId} in {ElapsedMs}ms", media.Id, lap.ElapsedMilliseconds);

        var sizes = new[]
        {
            ("sm", 320), ("md", 640), ("lg", 1280)
        };

        foreach (var (variant, width) in sizes)
        {
            lap.Restart();
            var thumb = await imageProcessor.ResizeAsync(image, width);
            var thumbPath = BlobPaths.ScreenshotThumb(message.MediaId, variant);
            await storage.UploadProcessedAsync(BlobContainers.Thumbnails, thumbPath, thumb, "image/webp");
            logger.LogDebug("Thumbnail {Variant} ({Width}px) for {MediaId} in {ElapsedMs}ms", variant, width, media.Id, lap.ElapsedMilliseconds);
        }

        stopwatch.Stop();

        logger.LogInformation("Image processing completed for {MediaId} in {ElapsedMs}ms — publishing ImageProcessed",
            media.Id, stopwatch.ElapsedMilliseconds);

        await storage.ProcessedImageUpdateAsync(media.Id, MediaStatus.Ready, image.Height, image.Width,
            fullWebp.Length);

        await publishEndpoint.Publish(new ImageProcessed
        {
            CorrelationId = message.CorrelationId,
            MediaId = message.MediaId
        });
    }
}