namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class GeoJsonPushRequested(EditBufferService editBuffer)
    : INotificationHandler<GeoJsonPushRequested.Notification>
{
    /// <summary>
    ///   Raised when the edit buffer changes and the full GeoJSON map sources need rebuilding.
    /// </summary>
    /// <param name="State">The current edit buffer state to push.</param>
    /// <param name="Sequence">Monotonically increasing counter used to discard superseded pushes.</param>
    public record Notification(EditBufferState State, int Sequence) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await editBuffer.RunPushGeoJsonNotificationAsync(notification.State, notification.Sequence);
    }
}
