using MassTransit;

namespace JSG.API.Stashframe.Core.Sagas.Contracts;

public record ProcessingTimedOut : CorrelatedBy<Guid>
{
    public Guid MediaId { get; set; }

    public Guid CorrelationId { get; set; }
}
