namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public class HandleNodeDragEnd(DrawingToolService drawing) : INotificationHandler<NodeDragEnded.Notification>
{
    /// <inheritdoc />
    public Task Handle(NodeDragEnded.Notification notification, CancellationToken cancellationToken)
        => drawing.HandleNodeDragEndAsync(notification.Event);
}
