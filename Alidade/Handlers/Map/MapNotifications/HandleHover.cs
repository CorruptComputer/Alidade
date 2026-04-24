namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public class HandleHover(DrawingToolService drawing) : INotificationHandler<HoverChanged.Notification>
{
    /// <inheritdoc />
    public Task Handle(HoverChanged.Notification notification, CancellationToken cancellationToken)
        => drawing.HandleHoverAsync(notification.ElementId);
}
