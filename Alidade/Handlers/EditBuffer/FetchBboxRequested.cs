namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class FetchBboxRequested(EditBufferService editBuffer, IMediator mediator)
    : INotificationHandler<FetchBboxRequested.Notification>
{
    /// <summary>
    ///   Raised when the viewport changes and OSM data for the bounding box should be fetched.
    /// </summary>
    /// <param name="Bounds">The map bounds to fetch data for.</param>
    public record Notification(MapBounds Bounds) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await editBuffer.RunFetchBboxAsync(notification.Bounds, mediator, cancellationToken);
    }
}
