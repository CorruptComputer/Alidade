namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class MergeNodes(EditBufferStateService editBufferState) : IRequestHandler<MergeNodes.Command, CommandResult>
{
    static MergeNodes() => UndoDescriptions.Register<Command>("Merge nodes");

    /// <summary>
    ///   Merges two nodes: replaces all way references to the source with the target,
    ///   then removes the source node.
    /// </summary>
    public record Command(long SourceNodeId, long TargetNodeId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Nodes.ContainsKey(request.SourceNodeId) || !state.Nodes.ContainsKey(request.TargetNodeId))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        // When one node is local (negative) and one is on the server (positive), always keep the server node.
        long sourceId = request.SourceNodeId > 0 && request.TargetNodeId < 0
            ? request.TargetNodeId
            : request.SourceNodeId;
        long targetId = sourceId == request.SourceNodeId ? request.TargetNodeId : request.SourceNodeId;

        ImmutableDictionary<long, OsmWay> ways = state.Ways;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        foreach ((long wayId, OsmWay way) in state.Ways)
        {
            if (!way.NodeIds.Contains(sourceId))
            {
                continue;
            }

            IReadOnlyList<long> newNodeIds = DeduplicateConsecutive([.. way.NodeIds.Select(id => id == sourceId ? targetId : id)]);
            OsmWay updated = way with { NodeIds = newNodeIds };
            ways = ways.SetItem(wayId, updated);

            EditState es = editStates.TryGetValue(updated.Ref, out EditState ex) && ex == EditState.Created
                ? EditState.Created
                : EditState.Modified;
            editStates = editStates.SetItem(updated.Ref, es);
        }

        OsmElementRef sourceRef = new(OsmElementTypes.Node, sourceId);
        editBufferState.SetState(state with
        {
            Nodes = state.Nodes.Remove(sourceId),
            Ways = ways,
            EditStates = editStates.Remove(sourceRef)
        });
        return Task.FromResult(CommandResult.Pass());
    }

    private static IReadOnlyList<long> DeduplicateConsecutive(IReadOnlyList<long> ids)
    {
        List<long> result = [];
        foreach (long id in ids)
        {
            if (result.Count == 0 || result[^1] != id)
            {
                result.Add(id);
            }
        }

        return result;
    }
}
