using JSG.API.Stashframe.Core.Constants;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Models;
using JSG.API.Stashframe.Core.Sagas.Contracts;
using MassTransit;
using Microsoft.AspNetCore.Mvc;

namespace JSG.API.Stashframe.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController(
    IMediaStorageService mediaStorageService,
    IPublishEndpoint publishEndpoint,
    ILogger<UploadController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> GenerateUploadUrl([FromBody] UploadRequest request)
    {
        logger.LogInformation("Upload requested for {FileName} ({ContentType}, {FileSize} bytes)", request.FileName,
            request.ContentType, request.FileSize);

        if (!SupportedMedia.IsSupported(request.ContentType))
        {
            logger.LogWarning("Rejected unsupported content type {ContentType} for file {FileName}",
                request.ContentType, request.FileName);
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

        logger.LogInformation("SAS URL generated for {MediaId} in container {Container}, category {Category}",
            sasResult.MediaId, sasResult.ContainerName, sasResult.Category);

        return Ok(sasResult);
    }

    [HttpPost("{mediaId:guid}/confirm")]
    public async Task<IActionResult> TriggerUploadCompleted([FromRoute] Guid mediaId)
    {
        var result = await mediaStorageService.ConfirmUploadAsync(mediaId);

        switch (result.Status)
        {
            case ConfirmUploadStatus.MediaNotFound:
                return NotFound("Media record not found.");
            case ConfirmUploadStatus.BlobNotFound:
                return BadRequest("Blob has not been uploaded.");
            case ConfirmUploadStatus.AlreadyClaimed:
                return Conflict("Upload already confirmed.");
        }

        await publishEndpoint.Publish(new UploadCompleted
        {
            MediaId = mediaId,
            Category = result.Category!.Value  // from DB, not client
        });

        return Ok();
    }
}