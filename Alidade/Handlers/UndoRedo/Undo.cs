using Alidade.Models.Undo;

namespace Alidade.Handlers.UndoRedo;

/// <inheritdoc />
public class Undo(UndoStateService undoState, EditBufferService editBuffer) : IRequestHandler<Undo.Command, CommandResult>
{
    /// <summary>
    ///   Pops the top action from the undo stack and applies it in reverse.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        IEditAction? action = undoState.PopUndo();
        if (action is null) return Task.FromResult(CommandResult.Pass());

        EditBufferState newState = action.Reverse(editBuffer.State);
        editBuffer.ReplaceState(newState);
        return Task.FromResult(CommandResult.Pass());
    }
}
