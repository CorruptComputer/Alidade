namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public sealed class RecordImageryUsed(EditBufferStateService editBufferState)
    : IRequestHandler<RecordImageryUsed.Command, CommandResult>
{
    /// <summary>
    ///   Appends an imagery source to the session imagery list. Duplicate names are ignored.
    /// </summary>
    /// <param name="Name">The human-readable name of the imagery source.</param>
    public record Command(string Name) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (state.ImageryUsed.Contains(request.Name))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        editBufferState.SetState(state with
        {
            ImageryUsed = state.ImageryUsed.Add(request.Name)
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
