namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class DeleteDraftRequested(IndexedDBService storage)
    : INotificationHandler<DeleteDraftRequested.Notification>
{
    /// <summary>
    ///   Raised when the edit buffer returns to a clean state and the persisted draft should be removed.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        await storage.DeleteDraftAsync();
    }
}
