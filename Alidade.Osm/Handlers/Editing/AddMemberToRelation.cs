namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class AddMemberToRelation(EditBufferStateService editBufferState)
    : IRequestHandler<AddMemberToRelation.Command, CommandResult>
{
    static AddMemberToRelation() => UndoDescriptions.Register<Command>("Add member to relation");

    /// <summary>
    ///   Appends an element as a new member of an existing relation.
    /// </summary>
    /// <param name="RelationId">The ID of the relation to update.</param>
    /// <param name="MemberRef">Reference to the element being added.</param>
    /// <param name="Role">The role the element will have within the relation.</param>
    public record Command(long RelationId, OsmElementRef MemberRef, string Role)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Relations.TryGetValue(request.RelationId, out OsmRelation? relation))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        OsmMember member = new(request.MemberRef.Type, request.MemberRef.Id, request.Role);
        OsmRelation updated = relation with { Members = [.. relation.Members, member] };
        OsmElementRef relRef = updated.Ref;
        EditState es = state.EditStates.TryGetValue(relRef, out EditState ex) && ex == EditState.Created
            ? EditState.Created
            : EditState.Modified;
        editBufferState.SetState(state with
        {
            Relations = state.Relations.SetItem(request.RelationId, updated),
            EditStates = state.EditStates.SetItem(relRef, es)
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
