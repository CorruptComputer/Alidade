namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public class HandleMapMoveEnd(MapStateService mapState) : INotificationHandler<MapMoveEnded.Notification>
{
    /// <inheritdoc />
    public Task Handle(MapMoveEnded.Notification notification, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { CurrentBounds = notification.Bounds });
        return Task.CompletedTask;
    }
}
