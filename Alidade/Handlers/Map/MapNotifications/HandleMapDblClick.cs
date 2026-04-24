namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public class HandleMapDblClick(DrawingToolService drawing) : INotificationHandler<MapDblClicked.Notification>
{
    /// <inheritdoc />
    public Task Handle(MapDblClicked.Notification notification, CancellationToken cancellationToken)
        => drawing.HandleMapDblClickAsync(notification.Event);
}
