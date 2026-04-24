namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class AddNodeToWay(EditBufferStateService editBufferState) : IRequestHandler<AddNodeToWay.Command, CommandResult>
{
    static AddNodeToWay() => UndoDescriptions.Register<Command>("Add node to way");

    /// <summary>
    ///   Inserts a node into an existing way's node list at the given index.
    /// </summary>
    public record Command(long WayId, long NodeId, int Index) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Ways.TryGetValue(request.WayId, out OsmWay? way))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        long[] newNodeIds = [.. way.NodeIds.Take(request.Index), request.NodeId, .. way.NodeIds.Skip(request.Index)];
        OsmWay updated = way with { NodeIds = newNodeIds };
        EditState es = state.EditStates.TryGetValue(updated.Ref, out EditState existing) && existing == EditState.Created
            ? EditState.Created
            : EditState.Modified;
        editBufferState.SetState(state with
        {
            Ways = state.Ways.SetItem(request.WayId, updated),
            EditStates = state.EditStates.SetItem(updated.Ref, es)
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
