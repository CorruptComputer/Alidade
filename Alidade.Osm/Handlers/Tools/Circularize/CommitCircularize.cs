using Alidade.Osm.Models.Tools.Circularize;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Circularize;

/// <inheritdoc />
public sealed class CommitCircularize(EditBufferStateService editBufferState)
    : IRequestHandler<CommitCircularize.Command, CommandResult>
{
    static CommitCircularize() => UndoDescriptions.Register<Command>("Circularize");

    /// <summary>
    ///   Applies pre-computed node position corrections to fit a way to a best-fit circle.
    ///   When <see cref="CircularizeResult.FinalWayNodeList"/> is non-empty, also creates any
    ///   new nodes and updates the target way's node list. The caller is responsible for
    ///   computing moves (e.g. via <see cref="CircularizeWay.Query"/>) and passing them in
    ///   directly so geometry is not recomputed on commit.
    /// </summary>
    /// <param name="Result">The pre-computed circularize geometry to commit.</param>
    /// <param name="WayId">
    ///   The ID of the way whose node list to update when new nodes are inserted. Required when
    ///   <see cref="CircularizeResult.FinalWayNodeList"/> is non-empty; otherwise unused.
    /// </param>
    public record Command(CircularizeResult Result, long? WayId = null)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<long, OsmNode> nodeDict = state.Nodes;
        ImmutableDictionary<long, OsmWay> wayDict = state.Ways;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;
        long nextId = state.NextNegativeId;

        // Tracks assigned IDs for new nodes in the order they appear in Moves (Old == null).
        List<long> newNodeActualIds = [];

        foreach ((long nodeId, Coordinate? old, Coordinate newLoc) in request.Result.Moves)
        {
            if (old is null)
            {
                OsmNode created = new(nextId, 1, null, null, null, newLoc.Y, newLoc.X,
                    ImmutableDictionary<string, string>.Empty);
                nodeDict = nodeDict.SetItem(nextId, created);
                editStates = editStates.SetItem(created.Ref, EditState.Created);
                newNodeActualIds.Add(nextId);
                nextId--;
            }
            else
            {
                if (!nodeDict.TryGetValue(nodeId, out OsmNode? node))
                {
                    continue;
                }
                nodeDict = nodeDict.SetItem(nodeId, node with { Lat = newLoc.Y, Lon = newLoc.X });
                EditState es = editStates.TryGetValue(node.Ref, out EditState ex) && ex == EditState.Created
                    ? EditState.Created
                    : EditState.Modified;
                editStates = editStates.SetItem(node.Ref, es);
            }
        }

        if (request.WayId is long wayId
            && request.Result.FinalWayNodeList.Count > 0
            && wayDict.TryGetValue(wayId, out OsmWay? way))
        {
            List<long> updatedNodeIds = [.. request.Result.FinalWayNodeList.Select(r =>
                r.IsNew ? newNodeActualIds[r.NewIndex] : r.ExistingId)];
            wayDict = wayDict.SetItem(wayId, way with { NodeIds = updatedNodeIds });
            EditState wayEs = editStates.TryGetValue(way.Ref, out EditState wex) && wex == EditState.Created
                ? EditState.Created
                : EditState.Modified;
            editStates = editStates.SetItem(way.Ref, wayEs);
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
