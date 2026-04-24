namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class SelectionPushRequested(EditBufferService editBuffer)
    : INotificationHandler<SelectionPushRequested.Notification>
{
    /// <summary>
    ///   Raised when a selection state push to the map sources is needed.
    /// </summary>
    public record Notification(int Sequence) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await editBuffer.RunSelectionPushAsync(notification.Sequence);
    }
}
