namespace Alidade.Handlers.UndoRedo;

/// <inheritdoc />
public class ToggleUndoHistory(UndoStateService undoState) : IRequestHandler<ToggleUndoHistory.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the undo history panel visibility.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        undoState.TogglePanelVisible();
        return Task.FromResult(CommandResult.Pass());
    }
}
