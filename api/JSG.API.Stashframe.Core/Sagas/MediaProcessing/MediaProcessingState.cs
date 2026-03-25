using JSG.API.Stashframe.Core.Enums;
using MassTransit;

namespace JSG.API.Stashframe.Core.Sagas.MediaProcessing;

// This is the state of the saga state machine
// correlationId is what makes the state unique.
// When sagas are triggered -> during processing refer to this context.
// ReSharper disable once ClassNeverInstantiated.Global
public class MediaProcessingState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;

    // Media info (captured when saga starts)
    public Guid MediaId { get; set; }
    public Guid UserId { get; set; }
    public string BlobPath { get; set; } = null!;
    public MediaCategory Category { get; set; }

    // Completion tracking (expand later for video)
    public bool ImageProcessed { get; set; }

    // Timeout token
    public Guid? TimeoutTokenId { get; set; }

    // Timestamps
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }

    // Redis requires saga state to implement ISagaVersion
    // MassTransit increments Version automatically on each state transition.
    public int Version { get; set; }
}
