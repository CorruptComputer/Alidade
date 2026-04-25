namespace Alidade.Osm.Handlers.Tagging;

/// <inheritdoc />
public sealed class BulkUpdateTags(EditBufferStateService editBufferState)
    : IRequestHandler<BulkUpdateTags.Command, CommandResult>
{
    static BulkUpdateTags() => UndoDescriptions.Register<Command>("Edit tags");

    /// <summary>
    ///   Replaces the tag set on each target element in a single atomic operation,
    ///   producing one entry on the undo stack regardless of how many elements are updated.
    /// </summary>
    /// <param name="Updates">
    ///   The list of elements to update, each paired with its complete new tag set.
    /// </param>
    public record Command(IReadOnlyList<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> Updates)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;

        foreach ((OsmElementRef target, IReadOnlyDictionary<string, string> newTags) in request.Updates)
        {
            ImmutableDictionary<string, string> tags = newTags.ToImmutableDictionary();

            EditState es = state.EditStates.TryGetValue(target, out EditState existing) && existing == EditState.Created
                ? EditState.Created
                : EditState.Modified;

            state = target.Type switch
            {
                OsmElementTypes.Node when state.Nodes.TryGetValue(target.Id, out OsmNode? n) =>
                    state with
                    {
                        Nodes = state.Nodes.SetItem(n.Id, n with { Tags = tags }),
                        EditStates = state.EditStates.SetItem(target, es)
                    },
                OsmElementTypes.Way when state.Ways.TryGetValue(target.Id, out OsmWay? w) =>
                    state with
                    {
                        Ways = state.Ways.SetItem(w.Id, w with { Tags = tags }),
                        EditStates = state.EditStates.SetItem(target, es)
                    },
                OsmElementTypes.Relation when state.Relations.TryGetValue(target.Id, out OsmRelation? r) =>
                    state with
                    {
                        Relations = state.Relations.SetItem(r.Id, r with { Tags = tags }),
                        EditStates = state.EditStates.SetItem(target, es)
                    },
                _ => state
            };
        }

        editBufferState.SetState(state);
        return Task.FromResult(CommandResult.Pass());
    }
}
