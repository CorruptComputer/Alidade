namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class GeoJsonPushRequested(EditBufferService editBuffer)
    : INotificationHandler<GeoJsonPushRequested.Notification>
{
    /// <summary>
    ///   Raised when the edit buffer changes and the full GeoJSON map sources need rebuilding.
    /// </summary>
    /// <param name="State">The current edit buffer state to push.</param>
    public record Notification(EditBufferState State) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await editBuffer.RunPushGeoJsonAsync(notification.State);
    }
}
