namespace JSG.API.Stashframe.Core.Sagas.Contracts;

public class MediaReady
{
    public Guid MediaId { get; set; }
    public Guid UserId { get; set; }
}
