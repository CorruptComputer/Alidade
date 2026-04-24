namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public class HandleMapClick(DrawingToolService drawing) : INotificationHandler<MapClicked.Notification>
{
    /// <inheritdoc />
    public Task Handle(MapClicked.Notification notification, CancellationToken cancellationToken)
        => drawing.HandleMapClickAsync(notification.Event);
}
