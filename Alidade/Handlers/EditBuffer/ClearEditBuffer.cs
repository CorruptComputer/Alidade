namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class ClearEditBuffer(EditBufferService editBuffer, UndoStateService undo)
    : IRequestHandler<ClearEditBuffer.Command, CommandResult>
{
    /// <summary>
    ///   Resets the edit buffer to its initial empty state and clears the undo/redo stacks.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        editBuffer.Clear();
        undo.Clear();
        return Task.FromResult(CommandResult.Pass());
    }
}
