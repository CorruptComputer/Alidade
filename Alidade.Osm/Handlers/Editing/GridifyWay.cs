namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public sealed class GridifyWay(EditBufferStateService editBufferState)
    : IRequestHandler<GridifyWay.Query, QueryResult<IReadOnlyList<OsmElementRef>>>
{
    static GridifyWay() => UndoDescriptions.Register<Query>("Gridify");

    /// <summary>
    ///   Splits a closed way into a grid of equal rectangular sub-areas.
    ///   The grid is rotated by <see cref="RotationDeg"/> degrees (clockwise from east in
    ///   the local flat-Earth projection). The original way is replaced by the new cell ways,
    ///   whose refs are returned on success.
    /// </summary>
    /// <param name="WayId">The ID of the closed way to gridify.</param>
    /// <param name="Rows">Number of rows in the output grid.</param>
    /// <param name="Cols">Number of columns in the output grid.</param>
    /// <param name="RotationDeg">Grid rotation in degrees.</param>
    public record Query(long WayId, int Rows, int Cols, double RotationDeg)
        : IRequest<QueryResult<IReadOnlyList<OsmElementRef>>>, IUndoableCommand;

    /// <inheritdoc />
    public Task<QueryResult<IReadOnlyList<OsmElementRef>>> Handle(Query request, CancellationToken cancellationToken)
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
            return Task.FromResult(QueryResult<IReadOnlyList<OsmElementRef>>.Fail("The selected way cannot be gridified. Select a single closed way."));
        }

        EditBufferState state = editBufferState.State;
        if (!state.Ways.TryGetValue(request.WayId, out OsmWay? originalWay))
        {
            return Task.FromResult(QueryResult<IReadOnlyList<OsmElementRef>>.Fail());
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
        List<OsmElementRef> cellRefs = [];
        foreach (IReadOnlyList<GridifyNodeRef> cellNodeRefs in result.CellNodeRefs)
        {
            long[] cellNodeIds = [.. cellNodeRefs.Select(r =>
                r.IsExisting ? r.ExistingNodeId : newNodeIds[r.NewNodeIndex])];
            OsmWay cellWay = new(nextId, 1, null, null, null, cellNodeIds, tags.ToImmutableDictionary());
            wayDict = wayDict.SetItem(nextId, cellWay);
            editStates = editStates.SetItem(cellWay.Ref, EditState.Created);
            cellRefs.Add(cellWay.Ref);
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

        return Task.FromResult<QueryResult<IReadOnlyList<OsmElementRef>>>(cellRefs);
    }
}
