namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CreateNode(EditBufferStateService editBufferState) : IRequestHandler<CreateNode.Command, CommandResult>
{
    static CreateNode() => UndoDescriptions.Register<Command>("Create node");

    /// <summary>
    ///   Creates a new OSM node at the given coordinates with the given tags.
    /// </summary>
    public record Command(double Lat, double Lon, IReadOnlyDictionary<string, string> Tags)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        long id = state.NextNegativeId;
        OsmNode node = new(id, 1, null, null, null, request.Lat, request.Lon, request.Tags.ToImmutableDictionary());
        editBufferState.SetState(state with
        {
            Nodes = state.Nodes.SetItem(id, node),
            EditStates = state.EditStates.SetItem(node.Ref, EditState.Created),
            NextNegativeId = id - 1
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
