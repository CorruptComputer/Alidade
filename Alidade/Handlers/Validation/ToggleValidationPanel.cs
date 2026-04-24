namespace Alidade.Handlers.Validation;

/// <inheritdoc />
public class ToggleValidationPanel(ValidationStateService validationState) : IRequestHandler<ToggleValidationPanel.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the validation panel visibility.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        validationState.SetState(validationState.State with { PanelVisible = !validationState.State.PanelVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
