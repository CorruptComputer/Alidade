namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class UpdateTags(EditBufferStateService editBufferState) : IRequestHandler<UpdateTags.Command, CommandResult>
{
    static UpdateTags() => UndoDescriptions.Register<Command>("Edit tags");

    /// <summary>
    ///   Replaces the tag set on the target element.
    /// </summary>
    public record Command(OsmElementRef Target, IReadOnlyDictionary<string, string> OldTags, IReadOnlyDictionary<string, string> NewTags)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<string, string> tags = request.NewTags.ToImmutableDictionary();

        EditState es = state.EditStates.TryGetValue(request.Target, out EditState existing) && existing == EditState.Created
            ? EditState.Created
            : EditState.Modified;

        EditBufferState newState = request.Target.Type switch
        {
            OsmElementTypes.Node when state.Nodes.TryGetValue(request.Target.Id, out OsmNode? n) =>
                state with
                {
                    Nodes = state.Nodes.SetItem(n.Id, n with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, es)
                },
            OsmElementTypes.Way when state.Ways.TryGetValue(request.Target.Id, out OsmWay? w) =>
                state with
                {
                    Ways = state.Ways.SetItem(w.Id, w with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, es)
                },
            OsmElementTypes.Relation when state.Relations.TryGetValue(request.Target.Id, out OsmRelation? r) =>
                state with
                {
                    Relations = state.Relations.SetItem(r.Id, r with { Tags = tags }),
                    EditStates = state.EditStates.SetItem(request.Target, es)
                },
            _ => state
        };

        editBufferState.SetState(newState);
        return Task.FromResult(CommandResult.Pass());
    }
}
