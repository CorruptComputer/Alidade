namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CreateWay(EditBufferStateService editBufferState) : IRequestHandler<CreateWay.Command, CommandResult>
{
    static CreateWay() => UndoDescriptions.Register<Command>("Create way");

    /// <summary>
    ///   Creates a new way with the given node IDs and tags.
    /// </summary>
    public record Command(IReadOnlyList<long> NodeIds, IReadOnlyDictionary<string, string> Tags)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        long id = state.NextNegativeId;
        OsmWay way = new(id, 1, null, null, null, [.. request.NodeIds], request.Tags.ToImmutableDictionary());
        editBufferState.SetState(state with
        {
            Ways = state.Ways.SetItem(id, way),
            EditStates = state.EditStates.SetItem(way.Ref, EditState.Created),
            NextNegativeId = id - 1
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
