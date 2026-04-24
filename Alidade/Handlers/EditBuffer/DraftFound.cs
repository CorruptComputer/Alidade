namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class DraftFound(DraftStateService draftState) : INotificationHandler<DraftFound.Notification>
{
    /// <summary>
    ///   Records that a recoverable draft is available and shows the recovery dialog.
    /// </summary>
    public record Notification(int DirtyCount, DateTimeOffset SavedAt) : NotificationBase;

    /// <inheritdoc />
    public Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        draftState.SetState(draftState.State with
        {
            HasDraft = true,
            DirtyCount = notification.DirtyCount,
            SavedAt = notification.SavedAt
        });
        return Task.CompletedTask;
    }
}
