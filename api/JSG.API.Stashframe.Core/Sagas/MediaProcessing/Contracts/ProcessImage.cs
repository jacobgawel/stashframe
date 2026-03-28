namespace JSG.API.Stashframe.Core.Sagas.MediaProcessing.Contracts;

public record ProcessImage
{
    // Commands the saga sends to consumers
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}
