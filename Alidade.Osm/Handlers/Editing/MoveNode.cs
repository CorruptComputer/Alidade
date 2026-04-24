namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class MoveNode(EditBufferStateService editBufferState) : IRequestHandler<MoveNode.Command, CommandResult>
{
    static MoveNode() => UndoDescriptions.Register<Command>("Move node");

    /// <summary>
    ///   Moves an existing node from its old coordinates to new ones.
    /// </summary>
    public record Command(long NodeId, double OldLat, double OldLon, double NewLat, double NewLon)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Nodes.TryGetValue(request.NodeId, out OsmNode? node))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        OsmNode updated = node with { Lat = request.NewLat, Lon = request.NewLon };
        EditState es = state.EditStates.TryGetValue(updated.Ref, out EditState existing) && existing == EditState.Created
            ? EditState.Created
            : EditState.Modified;

        editBufferState.SetState(state with
        {
            Nodes = state.Nodes.SetItem(request.NodeId, updated),
            EditStates = state.EditStates.SetItem(updated.Ref, es)
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
