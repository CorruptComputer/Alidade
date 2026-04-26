namespace Alidade.Handlers.Validation;

/// <inheritdoc />
public class ValidationStarted(ValidationStateService validationState) : INotificationHandler<ValidationStarted.Notification>
{
    /// <summary>
    ///   Marks validation as running.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        validationState.SetState(validationState.State with { IsRunning = true });
        return Task.CompletedTask;
    }
}
