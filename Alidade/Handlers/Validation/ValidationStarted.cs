using Microsoft.AspNetCore.Components;

namespace Alidade.Handlers.Validation;

/// <inheritdoc />
public class ValidationStarted(ValidationStateService validationState, Dispatcher dispatcher) : INotificationHandler<ValidationStarted.Notification>
{
    /// <summary>
    ///   Marks validation as running.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
        => await dispatcher.InvokeAsync(() => validationState.SetState(validationState.State with { IsRunning = true }));
}
