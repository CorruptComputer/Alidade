namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class ImageryLoadRequested(ImageryService imageryService)
    : INotificationHandler<ImageryLoadRequested.Notification>
{
    /// <summary>
    ///   Raised at startup to build NTS coverage geometries for the imagery layer catalog.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await imageryService.EnsureLoadedAsync();
    }
}
