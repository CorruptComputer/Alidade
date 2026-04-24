namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public sealed class GridifyWay(EditBufferStateService editBufferState)
    : IRequestHandler<GridifyWay.Command, CommandResult>
{
    static GridifyWay() => UndoDescriptions.Register<Command>("Gridify");

    /// <summary>
    ///   Splits a closed way into a grid of equal rectangular sub-areas.
    ///   The grid is rotated by <see cref="RotationDeg"/> degrees (clockwise from east in
    ///   the local flat-Earth projection). The original way is replaced by the new cell ways.
    /// </summary>
    /// <param name="WayId">The ID of the closed way to gridify.</param>
    /// <param name="Rows">Number of rows in the output grid.</param>
    /// <param name="Cols">Number of columns in the output grid.</param>
    /// <param name="RotationDeg">Grid rotation in degrees.</param>
    public record Command(long WayId, int Rows, int Cols, double RotationDeg)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        GridifyResult result = GeometryService.Gridify(
            request.WayId,
            editBufferState.State.Ways,
            editBufferState.State.Nodes,
            request.Rows,
            request.Cols,
            request.RotationDeg);

        if (result.CellNodeRefs.Count == 0)
        {
            return Task.FromResult(CommandResult.Fail("The selected way cannot be gridified. Select a single closed way."));
        }

        EditBufferState state = editBufferState.State;
        if (!state.Ways.TryGetValue(request.WayId, out OsmWay? originalWay))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        ImmutableDictionary<long, OsmNode> nodeDict = state.Nodes;
        ImmutableDictionary<long, OsmWay> wayDict = state.Ways;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;
        long nextId = state.NextNegativeId;

        long[] newNodeIds = new long[result.NewNodes.Count];
        for (int i = 0; i < result.NewNodes.Count; i++)
        {
            (double lat, double lon) = result.NewNodes[i];
            OsmNode node = new(nextId, 1, null, null, null, lat, lon, ImmutableDictionary<string, string>.Empty);
            nodeDict = nodeDict.SetItem(nextId, node);
            editStates = editStates.SetItem(node.Ref, EditState.Created);
            newNodeIds[i] = nextId;
            nextId--;
        }

        IReadOnlyDictionary<string, string> tags = originalWay.Tags;
        foreach (IReadOnlyList<GridifyNodeRef> cellRefs in result.CellNodeRefs)
        {
            long[] cellNodeIds = [.. cellRefs.Select(r =>
                r.IsExisting ? r.ExistingNodeId : newNodeIds[r.NewNodeIndex])];
            OsmWay cellWay = new(nextId, 1, null, null, null, cellNodeIds, tags.ToImmutableDictionary());
            wayDict = wayDict.SetItem(nextId, cellWay);
            editStates = editStates.SetItem(cellWay.Ref, EditState.Created);
            nextId--;
        }

        OsmElementRef wayRef = originalWay.Ref;
        EditState origEs = editStates.TryGetValue(wayRef, out EditState origExisting) ? origExisting : EditState.Fetched;
        if (origEs == EditState.Created)
        {
            wayDict = wayDict.Remove(request.WayId);
            editStates = editStates.Remove(wayRef);
        }
        else
        {
            editStates = editStates.SetItem(wayRef, EditState.Deleted);
        }

        editBufferState.SetState(state with
        {
            Nodes = nodeDict,
            Ways = wayDict,
            EditStates = editStates,
            NextNegativeId = nextId
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
