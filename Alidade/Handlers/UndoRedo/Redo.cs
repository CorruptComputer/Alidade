using Alidade.Models.Undo;

namespace Alidade.Handlers.UndoRedo;

/// <inheritdoc />
public class Redo(UndoStateService undoState, EditBufferService editBuffer) : IRequestHandler<Redo.Command, CommandResult>
{
    /// <summary>
    ///   Pops the top action from the redo stack and reapplies it forward.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        IEditAction? action = undoState.PopRedo();
        if (action is null) return Task.FromResult(CommandResult.Pass());

        EditBufferState newState = action.Apply(editBuffer.State);
        editBuffer.ReplaceState(newState);
        return Task.FromResult(CommandResult.Pass());
    }
}
