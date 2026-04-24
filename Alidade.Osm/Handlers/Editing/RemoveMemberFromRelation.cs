namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class RemoveMemberFromRelation(EditBufferStateService editBufferState)
    : IRequestHandler<RemoveMemberFromRelation.Command, CommandResult>
{
    static RemoveMemberFromRelation() => UndoDescriptions.Register<Command>("Remove member from relation");

    /// <summary>
    ///   Removes the first occurrence of an element from a relation's member list.
    /// </summary>
    /// <param name="RelationId">The ID of the relation to update.</param>
    /// <param name="MemberRef">Reference identifying the member to remove.</param>
    public record Command(long RelationId, OsmElementRef MemberRef)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Relations.TryGetValue(request.RelationId, out OsmRelation? relation))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        int idx = -1;
        for (int i = 0; i < relation.Members.Count; i++)
        {
            OsmMember m = relation.Members[i];
            if (m.Type == request.MemberRef.Type && m.Ref == request.MemberRef.Id) { idx = i; break; }
        }
        if (idx < 0)
        {
            return Task.FromResult(CommandResult.Pass());
        }

        OsmMember[] newMembers = [.. relation.Members.Take(idx), .. relation.Members.Skip(idx + 1)];
        OsmRelation updated = relation with { Members = newMembers };
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
