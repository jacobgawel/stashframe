using System.Diagnostics.CodeAnalysis;
using JSG.API.Stashframe.Core.Enums;
using JSG.API.Stashframe.Core.Sagas.Contracts;
using JSG.API.Stashframe.Core.Sagas.MediaProcessing.Contracts;
using MassTransit;

namespace JSG.API.Stashframe.Core.Sagas.MediaProcessing;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class MediaProcessingSaga : MassTransitStateMachine<MediaProcessingState>
{
    // States
    public State Processing { get; set; } = null!;
    public State Failed { get; set; } = null!;


    // Events
    public Event<UploadCompleted> UploadCompleted { get; set; } = null!;

    public Event<ImageProcessed> ImageProcessed { get; set; } = null!;

    // Timeout schedule
    public Schedule<MediaProcessingState, ProcessingTimedOut> ProcessingTimeout { get; set; } = null!;


    public MediaProcessingSaga()
    {
        InstanceState(x => x.CurrentState);

        // UploadCompleted uses MediaId as the saga identifier
        Event(() => UploadCompleted, x => 
            x.CorrelateById(ctx => ctx.Message.MediaId));

        // Completion events correlate by CorrelationId
        Event(() => ImageProcessed, x =>
            x.CorrelateById(ctx => ctx.Message.CorrelationId));

        // Configure the timeout
        Schedule(() => ProcessingTimeout, x => x.TimeoutTokenId,
            s =>
            {
                s.Delay = TimeSpan.FromMinutes(5);
                s.Received = e => e.CorrelateById(ctx => ctx.Message.CorrelationId);
            });

        // === State transitions ===
        Initially(
            When(UploadCompleted)
                .Then(ctx =>
                {
                    // Populating the saga values
                    ctx.Saga.MediaId = ctx.Message.MediaId;
                    ctx.Saga.Category = ctx.Message.Category;
                    ctx.Saga.Created = DateTime.UtcNow;
                    ctx.Saga.Updated = DateTime.UtcNow;
                })
                .If(ctx => ctx.Message.Category == MediaCategory.Screenshot,
                    image => image.Publish(ctx => new ProcessImage
                    {
                        MediaId = ctx.Saga.MediaId,
                        CorrelationId = ctx.Saga.CorrelationId,
                    }))
                .Schedule(ProcessingTimeout, ctx => new ProcessingTimedOut
                {
                    MediaId = ctx.Saga.MediaId
                })
                .TransitionTo(Processing)
        );

        During(Processing,
            When(ImageProcessed)
                .Unschedule(ProcessingTimeout)
                .Publish(ctx => new MediaReady
                {
                    // this will be consumed via the signalR service later
                    MediaId = ctx.Saga.MediaId,
                    UserId = ctx.Saga.UserId
                })
                .Finalize(),
            When(ProcessingTimeout.Received)
                .Then(ctx => ctx.Saga.Updated = DateTime.UtcNow)
                .TransitionTo(Failed)
        );

        SetCompletedWhenFinalized();
    }
}