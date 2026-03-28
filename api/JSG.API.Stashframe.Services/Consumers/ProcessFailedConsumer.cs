using System.Diagnostics.CodeAnalysis;
using JSG.API.Stashframe.Core.Interfaces.Services;
using JSG.API.Stashframe.Core.Sagas.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace JSG.API.Stashframe.Services.Consumers;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class ProcessFailedConsumer(ILogger<ProcessFailedConsumer> logger, IMediaStorageService storage)
    : IConsumer<FailedProcessing>
{
    public async Task Consume(ConsumeContext<FailedProcessing> context)
    {
        var message = context.Message;

        logger.LogInformation("Processing failed media {MediaId} (CorrelationId: {CorrelationId})", message.MediaId,
            message.CorrelationId);
        
        var media = await storage.GetMediaAsync(message.MediaId);

        if (media is null)
        {
            logger.LogWarning("Media {MediaId} not found in database — skipping processing", message.MediaId);
            return;
        }

        await storage.UpdateToFailedAsync(message.MediaId);
    }
}