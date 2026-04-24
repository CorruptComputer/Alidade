namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class DraftResolved(DraftStateService draftState) : INotificationHandler<DraftResolved.Notification>
{
    /// <summary>
    ///   Clears the draft availability flag after the user continues or discards.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        draftState.SetState(new DraftState());
        return Task.CompletedTask;
    }
}
