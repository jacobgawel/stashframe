namespace JSG.API.Stashframe.Core.Sagas.Contracts;

public record FailedProcessing
{
    public Guid MediaId { get; set; }
    public Guid CorrelationId { get; set; }
}