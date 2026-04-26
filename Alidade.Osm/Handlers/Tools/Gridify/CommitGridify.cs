using Alidade.Osm.Models.Tools.Gridify;

namespace Alidade.Osm.Handlers.Tools.Gridify;

/// <inheritdoc />
public sealed class CommitGridify(EditBufferStateService editBufferState)
    : IRequestHandler<CommitGridify.Query, QueryResult<IReadOnlyList<OsmElementRef>>>
{
    static CommitGridify() => UndoDescriptions.Register<Query>("Gridify");

    /// <summary>
    ///   Commits a pre-computed <see cref="GridifyResult"/> to the edit buffer, replacing the
    ///   original way with the generated cell ways. The caller is responsible for computing the
    ///   result (e.g. via <see cref="GridifyWay.Query"/>) and passing it in directly so the
    ///   geometry is not recomputed on apply.
    /// </summary>
    /// <param name="WayId">The ID of the original closed way to replace.</param>
    /// <param name="Result">The pre-computed grid geometry to commit.</param>
    public record Query(long WayId, GridifyResult Result)
        : IRequest<QueryResult<IReadOnlyList<OsmElementRef>>>, IUndoableCommand;

    /// <inheritdoc />
    public Task<QueryResult<IReadOnlyList<OsmElementRef>>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.Result.CellNodeRefs.Count == 0)
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

        long[] newNodeIds = new long[request.Result.NewNodes.Count];
        for (int i = 0; i < request.Result.NewNodes.Count; i++)
        {
            (double lat, double lon) = request.Result.NewNodes[i];
            OsmNode node = new(nextId, 1, null, null, null, lat, lon, ImmutableDictionary<string, string>.Empty);
            nodeDict = nodeDict.SetItem(nextId, node);
            editStates = editStates.SetItem(node.Ref, EditState.Created);
            newNodeIds[i] = nextId;
            nextId--;
        }

        IReadOnlyDictionary<string, string> tags = originalWay.Tags;
        List<OsmElementRef> cellRefs = [];
        foreach (IReadOnlyList<GridifyNodeRef> cellNodeRefs in request.Result.CellNodeRefs)
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
