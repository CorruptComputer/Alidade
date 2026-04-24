namespace Alidade.PipelineBehaviors;

/// <summary>
///   Pipeline behavior that automatically snapshots the edit buffer before and after
///   any command implementing <see cref="IUndoableCommand"/>, then pushes an entry
///   onto the undo stack via <see cref="UndoStateService"/>.
/// </summary>
public class UndoPipelineBehavior<TRequest, TResponse>(
    EditBufferStateService editBuffer,
    UndoStateService undo)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IUndoableCommand, IRequest<TResponse>
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        EditBufferState before = editBuffer.State;
        string description = UndoDescriptions.For<TRequest>();
        TResponse response = await next(cancellationToken);
        EditBufferState after = editBuffer.State;
        undo.Push(new Alidade.Models.Undo.EditBufferSnapshot(description, before, after));
        return response;
    }
}

