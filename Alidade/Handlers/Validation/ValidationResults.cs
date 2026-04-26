namespace Alidade.Handlers.Validation;

/// <inheritdoc />
public class ValidationResults(ValidationStateService validationState) : INotificationHandler<ValidationResults.Notification>
{
    /// <summary>
    ///   Replaces the issue list with the results of a completed validation pass.
    /// </summary>
    public record Notification(IReadOnlyList<ValidationIssue> Issues) : NotificationBase;

    /// <inheritdoc />
    public Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        validationState.SetState(validationState.State with
        {
            Issues = [.. notification.Issues],
            IsRunning = false
        });
        return Task.CompletedTask;
    }
}
