namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class SaveDraftRequested(EditBufferService editBuffer)
    : INotificationHandler<SaveDraftRequested.Notification>
{
    /// <summary>
    ///   Raised when the edit buffer becomes dirty and a debounced draft save should run.
    /// </summary>
    /// <param name="State">The edit buffer state to persist.</param>
    public record Notification(EditBufferState State) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await editBuffer.RunSaveDraftDebounced(notification.State, cancellationToken);
    }
}
