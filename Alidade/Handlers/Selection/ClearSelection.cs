namespace Alidade.Handlers.Selection;

/// <inheritdoc />
public class ClearSelection(SelectionStateService selectionState) : IRequestHandler<ClearSelection.Command, CommandResult>
{
    /// <summary>
    ///   Clears the selection set.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        selectionState.SetState(selectionState.State with { Selected = [] });
        return Task.FromResult(CommandResult.Pass());
    }
}
