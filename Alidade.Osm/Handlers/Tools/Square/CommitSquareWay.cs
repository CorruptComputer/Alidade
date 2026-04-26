using Alidade.Osm.Models.Tools.Square;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class CommitSquareWay(EditBufferStateService editBufferState)
    : IRequestHandler<CommitSquareWay.Command, CommandResult>
{
    static CommitSquareWay() => UndoDescriptions.Register<Command>("Square Way");

    /// <summary>
    ///   Applies pre-computed node position corrections to square a way's corners.
    ///   The caller is responsible for computing moves (e.g. via <see cref="SquareWay.Query"/>)
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
