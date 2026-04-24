namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class SplitWayAtNode(EditBufferStateService editBufferState) : IRequestHandler<SplitWayAtNode.Command, CommandResult>
{
    static SplitWayAtNode() => UndoDescriptions.Register<Command>("Split way");

    /// <summary>
    ///   Splits a way at the specified node, producing two ways sharing that node.
    /// </summary>
    public record Command(long WayId, long AtNodeId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Ways.TryGetValue(request.WayId, out OsmWay? way))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        int splitIdx = -1;
        for (int idx = 0; idx < way.NodeIds.Count; idx++)
        {
            if (way.NodeIds[idx] == request.AtNodeId) { splitIdx = idx; break; }
        }
        if (splitIdx <= 0 || splitIdx >= way.NodeIds.Count - 1)
        {
            return Task.FromResult(CommandResult.Pass());
        }

        long[] firstIds = [.. way.NodeIds.Take(splitIdx + 1)];
        long[] secondIds = [.. way.NodeIds.Skip(splitIdx)];

        EditState originalEs = state.EditStates.TryGetValue(way.Ref, out EditState origEs) ? origEs : EditState.Fetched;

        OsmWay first = way with { NodeIds = firstIds };
        EditState firstEs = originalEs == EditState.Created ? EditState.Created : EditState.Modified;

        long newId = state.NextNegativeId;
        OsmWay second = new(newId, 1, null, null, null, secondIds, way.Tags);

        editBufferState.SetState(state with
        {
            Ways = state.Ways.SetItem(request.WayId, first).SetItem(newId, second),
            EditStates = state.EditStates
                .SetItem(first.Ref, firstEs)
                .SetItem(second.Ref, EditState.Created),
            NextNegativeId = newId - 1
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
