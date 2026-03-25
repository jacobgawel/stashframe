using JSG.API.Stashframe.Core.Constants;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Models;
using JSG.API.Stashframe.Core.Sagas.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace JSG.API.Stashframe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController(IMediaStorageService mediaStorageService, IPublishEndpoint publishEndpoint, ILogger<UploadController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> GenerateUploadUrl([FromBody] UploadRequest request)
    {
        logger.LogInformation("Upload requested for {FileName} ({ContentType}, {FileSize} bytes)", request.FileName, request.ContentType, request.FileSize);

        if (!SupportedMedia.IsSupported(request.ContentType, out var category))
        {
            logger.LogWarning("Rejected unsupported content type {ContentType} for file {FileName}", request.ContentType, request.FileName);
            return BadRequest(new
            {
                Error = $"Content type '{request.ContentType}' is not supported.",
                SupportedTypes = SupportedMedia.MimeTypes.Keys
            });
        }

        var sasResult = await mediaStorageService.GenerateUploadSasAsync(
            request.ContentType,
            request.FileName,
            request.FileSize,
            TimeSpan.FromMinutes(15));

        logger.LogInformation("SAS URL generated for {MediaId} in container {Container}, category {Category}", sasResult.MediaId, sasResult.ContainerName, sasResult.Category);

        return Ok(sasResult);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> TriggerUploadCompleted([FromBody] UploadCompleted completed)
    {
        logger.LogInformation("Upload confirmation received for {MediaId}, category {Category}", completed.MediaId, completed.Category);

        await publishEndpoint.Publish(completed);

        logger.LogInformation("UploadCompleted event published for {MediaId}", completed.MediaId);

        return Ok();
    }
}
