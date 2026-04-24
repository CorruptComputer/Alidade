namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class DeleteNode(EditBufferStateService editBufferState) : IRequestHandler<DeleteNode.Command, CommandResult>
{
    static DeleteNode() => UndoDescriptions.Register<Command>("Delete node");

    /// <summary>
    ///   Marks a node as deleted (or removes it if locally created).
    /// </summary>
    public record Command(long NodeId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        OsmElementRef nodeRef = new(OsmElementTypes.Node, request.NodeId);
        state.EditStates.TryGetValue(nodeRef, out EditState existing);

        ImmutableDictionary<OsmElementRef, EditState> es = existing == EditState.Created
            ? state.EditStates.Remove(nodeRef)
            : state.EditStates.SetItem(nodeRef, EditState.Deleted);

        ImmutableDictionary<long, OsmNode> nodes = existing == EditState.Created
            ? state.Nodes.Remove(request.NodeId)
            : state.Nodes;

        ImmutableDictionary<long, OsmWay> ways = state.Ways;
        foreach (OsmWay way in state.Ways.Values)
        {
            if (!way.NodeIds.Contains(request.NodeId))
            {
                continue;
            }

            IReadOnlyList<long> cleaned = [.. way.NodeIds.Where(id => id != request.NodeId)];
            OsmElementRef wayRef = way.Ref;
            state.EditStates.TryGetValue(wayRef, out EditState wayState);

            if (cleaned.Count < 2)
            {
                if (wayState == EditState.Created)
                {
                    ways = ways.Remove(way.Id);
                    es = es.Remove(wayRef);
                }
                else
                {
                    es = es.SetItem(wayRef, EditState.Deleted);
                }
            }
            else
            {
                ways = ways.SetItem(way.Id, way with { NodeIds = cleaned });
            }
        }

        editBufferState.SetState(state with { Nodes = nodes, Ways = ways, EditStates = es });
        return Task.FromResult(CommandResult.Pass());
    }
}
