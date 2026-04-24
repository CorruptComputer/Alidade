using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Models.Editing;

namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class FetchElementRequested(EditBufferService editBuffer, IMediator mediator)
    : INotificationHandler<FetchElementRequested.Notification>
{
    /// <summary>
    ///   Raised when a single OSM element should be fetched from the API and merged into the edit buffer.
    /// </summary>
    /// <param name="ElementRef">Reference identifying the element to fetch.</param>
    public record Notification(OsmElementRef ElementRef) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        FetchElementResult? result = await mediator.Send(
            new FetchElement.Query(notification.ElementRef), cancellationToken);

        if (result is not null)
        {
            editBuffer.MergeFetchedData(
                (IReadOnlyList<OsmNode>)result.Nodes,
                (IReadOnlyList<OsmWay>)result.Ways,
                (IReadOnlyList<OsmRelation>)result.Relations);
        }
    }
}
