namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class DeleteWay(EditBufferStateService editBufferState) : IRequestHandler<DeleteWay.Command, CommandResult>
{
    static DeleteWay() => UndoDescriptions.Register<Command>("Delete way");

    /// <summary>
    ///   Marks a way as deleted (or removes it if locally created).
    ///   Nodes that belonged exclusively to this way and carry no tags are deleted along with it.
    /// </summary>
    public record Command(long WayId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        OsmElementRef wayRef = new(OsmElementTypes.Way, request.WayId);
        state.EditStates.TryGetValue(wayRef, out EditState existing);

        ImmutableDictionary<OsmElementRef, EditState> es = existing == EditState.Created
            ? state.EditStates.Remove(wayRef)
            : state.EditStates.SetItem(wayRef, EditState.Deleted);

        ImmutableDictionary<long, OsmWay> ways = existing == EditState.Created
            ? state.Ways.Remove(request.WayId)
            : state.Ways;

        ImmutableDictionary<long, OsmNode> nodes = state.Nodes;

        if (state.Ways.TryGetValue(request.WayId, out OsmWay? deletedWay))
        {
            HashSet<long> wayNodeIds = [.. deletedWay.NodeIds];

            Dictionary<long, int> sharedCount = [];
            foreach (OsmWay w in state.Ways.Values)
            {
                if (w.Id == request.WayId) { continue; }
                if (state.EditStates.GetValueOrDefault(w.Ref) == EditState.Deleted) { continue; }

                HashSet<long> seen = [];
                foreach (long nodeId in w.NodeIds)
                {
                    if (wayNodeIds.Contains(nodeId) && seen.Add(nodeId))
                    {
                        sharedCount[nodeId] = sharedCount.GetValueOrDefault(nodeId) + 1;
                    }
                }
            }

            foreach (long nodeId in wayNodeIds)
            {
                if (sharedCount.ContainsKey(nodeId)) { continue; }
                if (!nodes.TryGetValue(nodeId, out OsmNode? node)) { continue; }
                if (node.Tags.Count > 0) { continue; }

                OsmElementRef nodeRef = new(OsmElementTypes.Node, nodeId);
                state.EditStates.TryGetValue(nodeRef, out EditState nodeExisting);

                if (nodeExisting == EditState.Created)
                {
                    es = es.Remove(nodeRef);
                    nodes = nodes.Remove(nodeId);
                }
                else
                {
                    es = es.SetItem(nodeRef, EditState.Deleted);
                }
            }
        }

        editBufferState.SetState(state with { Nodes = nodes, Ways = ways, EditStates = es });
        return Task.FromResult(CommandResult.Pass());
    }
}
