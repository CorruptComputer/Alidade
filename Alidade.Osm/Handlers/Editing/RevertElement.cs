namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class RevertElement(EditBufferStateService editBufferState) : IRequestHandler<RevertElement.Command, CommandResult>
{
    /// <summary>
    ///   Reverts an element to the server's tag set and marks it as Fetched.
    /// </summary>
    public record Command(OsmElementRef Target, IReadOnlyDictionary<string, string> ServerTags)
        : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<string, string> tags = request.ServerTags.ToImmutableDictionary();
        EditBufferState newState = request.Target.Type switch
        {
            OsmElementTypes.Node when state.Nodes.TryGetValue(request.Target.Id, out OsmNode? n) =>
                state with
                {
                    Nodes = state.Nodes.SetItem(n.Id, n with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, EditState.Fetched)
                },
            OsmElementTypes.Way when state.Ways.TryGetValue(request.Target.Id, out OsmWay? w) =>
                state with
                {
                    Ways = state.Ways.SetItem(w.Id, w with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, EditState.Fetched)
                },
            OsmElementTypes.Relation when state.Relations.TryGetValue(request.Target.Id, out OsmRelation? r) =>
                state with
                {
                    Relations = state.Relations.SetItem(r.Id, r with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, EditState.Fetched)
                },
            _ => state
        };
        editBufferState.SetState(newState);
        return Task.FromResult(CommandResult.Pass());
    }
}
