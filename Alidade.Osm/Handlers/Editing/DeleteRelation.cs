namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class DeleteRelation(EditBufferStateService editBufferState) : IRequestHandler<DeleteRelation.Command, CommandResult>
{
    static DeleteRelation() => UndoDescriptions.Register<Command>("Delete relation");

    /// <summary>
    ///   Marks a relation as deleted (or removes it if locally created).
    /// </summary>
    public record Command(long RelationId) : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        OsmElementRef relRef = new(OsmElementTypes.Relation, request.RelationId);
        state.EditStates.TryGetValue(relRef, out EditState existing);

        ImmutableDictionary<OsmElementRef, EditState> es = existing == EditState.Created
            ? state.EditStates.Remove(relRef)
            : state.EditStates.SetItem(relRef, EditState.Deleted);

        ImmutableDictionary<long, OsmRelation> relations = existing == EditState.Created
            ? state.Relations.Remove(request.RelationId)
            : state.Relations;

        editBufferState.SetState(state with { Relations = relations, EditStates = es });
        return Task.FromResult(CommandResult.Pass());
    }
}
