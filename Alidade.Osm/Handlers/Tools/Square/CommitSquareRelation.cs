using Alidade.Osm.Models.Tools.Square;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class CommitSquareRelation(EditBufferStateService editBufferState)
    : IRequestHandler<CommitSquareRelation.Command, CommandResult>
{
    static CommitSquareRelation() => UndoDescriptions.Register<Command>("Square Relation");

    /// <summary>
    ///   Applies pre-computed node position corrections to square the member ways of a relation.
    ///   The caller is responsible for computing moves (e.g. via <see cref="SquareRelation.Query"/>)
    ///   and passing them in directly so geometry is not recomputed on commit.
    /// </summary>
    /// <param name="Result">The pre-computed square geometry to commit.</param>
    public record Command(SquareResult Result) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<long, OsmNode> nodes = state.Nodes;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        foreach ((long nodeId, Coordinate newLoc) in request.Result.Moves)
        {
            if (!nodes.TryGetValue(nodeId, out OsmNode? node))
            {
                continue;
            }
            nodes = nodes.SetItem(nodeId, node with { Lat = newLoc.Y, Lon = newLoc.X });
            EditState es = editStates.TryGetValue(node.Ref, out EditState ex) && ex == EditState.Created
                ? EditState.Created
                : EditState.Modified;
            editStates = editStates.SetItem(node.Ref, es);
        }

        editBufferState.SetState(state with { Nodes = nodes, EditStates = editStates });
        return Task.FromResult(CommandResult.Pass());
    }
}
