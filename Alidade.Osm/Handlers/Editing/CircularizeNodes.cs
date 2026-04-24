namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CircularizeNodes(EditBufferStateService editBufferState) : IRequestHandler<CircularizeNodes.Command, CommandResult>
{
    static CircularizeNodes() => UndoDescriptions.Register<Command>("Circularize");

    /// <summary>
    ///   Applies pre-computed node position corrections to fit a closed way to a best-fit circle.
    /// </summary>
    public record Command(IReadOnlyList<(long NodeId, double OldLat, double OldLon, double NewLat, double NewLon)> Moves)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<long, OsmNode> nodes = state.Nodes;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        foreach ((long nodeId, _, _, double newLat, double newLon) in request.Moves)
        {
            if (!nodes.TryGetValue(nodeId, out OsmNode? node)) { continue; }
            nodes = nodes.SetItem(nodeId, node with { Lat = newLat, Lon = newLon });
            EditState es = editStates.TryGetValue(node.Ref, out EditState ex) && ex == EditState.Created
                ? EditState.Created
                : EditState.Modified;
            editStates = editStates.SetItem(node.Ref, es);
        }

        editBufferState.SetState(state with { Nodes = nodes, EditStates = editStates });
        return Task.FromResult(CommandResult.Pass());
    }
}
