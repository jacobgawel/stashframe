using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace JSG.API.Stashframe.Services;

public class ImageProcessingService(ILogger<ImageProcessingService> logger) : IImageProcessingService
{
    public async Task<Stream> CropToAspectAsync(Image image, int width, int height)
    {
        logger.LogDebug("Cropping image to {Width}x{Height}", width, height);

        var clone = image.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
            });
        });

        var output = new MemoryStream();

        await clone.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 });

        output.Position = 0;

        logger.LogDebug("Crop complete — output {OutputSize} bytes", output.Length);

        return output;
    }

    public async Task<Stream> OptimiseAsync(Image image, OutputFormat format)
    {
        logger.LogDebug("Optimising image ({SourceWidth}x{SourceHeight}) to {Format}", image.Width, image.Height,
            format);

        var output = new MemoryStream();

        switch (format)
        {
            case OutputFormat.WebP:
                await image.SaveAsWebpAsync(output, new WebpEncoder { Quality = 85 });
                break;
            case OutputFormat.Jpeg:
                await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = 85 });
                break;
            case OutputFormat.Png:
                await image.SaveAsPngAsync(output);
                break;
        }

        output.Position = 0;

        logger.LogDebug("Optimisation complete — {Format}, {OutputSize} bytes", format, output.Length);

        return output;
    }

    public async Task<Stream> ResizeAsync(Image image, int targetWidth)
    {
        var ratio = (double)targetWidth / image.Width;
        var targetHeight = (int)(image.Height * ratio);

        logger.LogDebug("Resizing image from {SourceWidth}x{SourceHeight} to {TargetWidth}x{TargetHeight}", image.Width,
            image.Height, targetWidth, targetHeight);

        var clone = image.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ResizeMode.Max,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var output = new MemoryStream();

        await clone.SaveAsWebpAsync(output, new WebpEncoder { Quality = 80 });

        output.Position = 0;

        logger.LogDebug("Resize complete — {TargetWidth}x{TargetHeight}, {OutputSize} bytes", targetWidth, targetHeight,
            output.Length);

        return output;
    }
}