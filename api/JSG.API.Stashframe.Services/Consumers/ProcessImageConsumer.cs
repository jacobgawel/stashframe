using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JSG.API.Stashframe.Core.Constants;
using JSG.API.Stashframe.Core.Database;
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
    IImageProcessingService imageProcessor,
    StashframeContext databaseContext) : IConsumer<ProcessImage>
{
    public async Task Consume(ConsumeContext<ProcessImage> context)
    {
        var message = context.Message;
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Processing image for {MediaId} (CorrelationId: {CorrelationId})", message.MediaId,
            message.CorrelationId);

        var media = await databaseContext.Media.FindAsync(message.MediaId);

        if (media is null)
        {
            logger.LogWarning("Media {MediaId} not found in database — skipping processing", message.MediaId);
            return;
        }

        logger.LogInformation("Downloading raw blob for {MediaId} from {BlobPath}", media.Id, media.RawBlobPath);
        await using var rawStream = await storage.DownloadAsync(BlobContainers.Raw, media.RawBlobPath);

        using var image = await Image.LoadAsync(rawStream);
        logger.LogInformation("Image loaded for {MediaId} — {Width}x{Height}", media.Id, image.Width, image.Height);

        media.Width = image.Width;
        media.Height = image.Height;

        logger.LogInformation("Optimising full-size WebP for {MediaId}", media.Id);
        var fullWebp = await imageProcessor.OptimiseAsync(image, OutputFormat.WebP);
        var fullPath = BlobPaths.ScreenshotFull(message.MediaId);

        await storage.UploadProcessedAsync(BlobContainers.Screenshots, fullPath, fullWebp, "image/webp");

        var sizes = new[]
        {
            ("sm", 320), ("md", 640), ("lg", 1280)
        };

        foreach (var (variant, width) in sizes)
        {
            logger.LogInformation("Generating {Variant} thumbnail ({Width}px) for {MediaId}", variant, width, media.Id);
            var thumb = await imageProcessor.ResizeAsync(image, width);
            var thumbPath = BlobPaths.ScreenshotThumb(message.MediaId, variant);
            await storage.UploadProcessedAsync(BlobContainers.Thumbnails, thumbPath, thumb, "image/webp");
        }

        stopwatch.Stop();

        logger.LogInformation("Image processing completed for {MediaId} in {ElapsedMs}ms — publishing ImageProcessed",
            media.Id, stopwatch.ElapsedMilliseconds);

        await publishEndpoint.Publish(new ImageProcessed
        {
            CorrelationId = message.CorrelationId,
            MediaId = message.MediaId
        });
    }
}