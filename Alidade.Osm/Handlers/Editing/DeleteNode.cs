namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class DeleteNode(EditBufferStateService editBufferState) : IRequestHandler<DeleteNode.Command, CommandResult>
{
    static DeleteNode() => UndoDescriptions.Register<Command>("Delete node");

    /// <summary>
    ///   Marks a node as deleted (or removes it if locally created).
    /// </summary>
    public record Command(long NodeId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        OsmElementRef nodeRef = new(OsmElementTypes.Node, request.NodeId);
        state.EditStates.TryGetValue(nodeRef, out EditState existing);

        ImmutableDictionary<OsmElementRef, EditState> es = existing == EditState.Created
            ? state.EditStates.Remove(nodeRef)
            : state.EditStates.SetItem(nodeRef, EditState.Deleted);

        ImmutableDictionary<long, OsmNode> nodes = existing == EditState.Created
            ? state.Nodes.Remove(request.NodeId)
            : state.Nodes;

        editBufferState.SetState(state with { Nodes = nodes, EditStates = es });
        return Task.FromResult(CommandResult.Pass());
    }
}
