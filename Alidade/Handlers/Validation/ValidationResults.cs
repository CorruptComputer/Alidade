using Microsoft.AspNetCore.Components;

namespace Alidade.Handlers.Validation;

/// <inheritdoc />
public class ValidationResults(ValidationStateService validationState, Dispatcher dispatcher) : INotificationHandler<ValidationResults.Notification>
{
    /// <summary>
    ///   Replaces the issue list with the results of a completed validation pass.
    /// </summary>
    public record Notification(IReadOnlyList<ValidationIssue> Issues) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
        => await dispatcher.InvokeAsync(() => validationState.SetState(validationState.State with
        {
            Issues = [.. notification.Issues],
            IsRunning = false
        }));
}
