namespace JSG.API.Stashframe.Core.Sagas.MediaProcessing.Contracts;

public record ImageProcessed
{
    // Events consumers publish back to the saga
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
