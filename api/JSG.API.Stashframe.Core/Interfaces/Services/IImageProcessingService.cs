using JSG.API.Stashframe.Core.Enums;
using SixLabors.ImageSharp;

namespace JSG.API.Stashframe.Core.Interfaces.Services;

public interface IImageProcessingService
{
    Task<Stream> OptimiseAsync(Image image, OutputFormat format);
    Task<Stream> ResizeAsync(Image image, int targetWidth);
    Task<Stream> CropToAspectAsync(Image image, int width, int height);
}
