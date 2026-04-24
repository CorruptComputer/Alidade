namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class ReorderRelationMember(EditBufferStateService editBufferState)
    : IRequestHandler<ReorderRelationMember.Command, CommandResult>
{
    static ReorderRelationMember() => UndoDescriptions.Register<Command>("Reorder relation member");

    /// <summary>
    ///   Moves a member from one position to another within a relation's ordered member list.
    /// </summary>
    /// <param name="RelationId">The ID of the relation to update.</param>
    /// <param name="FromIndex">Zero-based index of the member to move.</param>
    /// <param name="ToIndex">Target zero-based index for the member.</param>
    public record Command(long RelationId, int FromIndex, int ToIndex)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        if (!state.Relations.TryGetValue(request.RelationId, out OsmRelation? relation))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        if (request.FromIndex < 0 || request.FromIndex >= relation.Members.Count) { return Task.FromResult(CommandResult.Pass()); }
        if (request.ToIndex < 0 || request.ToIndex >= relation.Members.Count) { return Task.FromResult(CommandResult.Pass()); }
        if (request.FromIndex == request.ToIndex) { return Task.FromResult(CommandResult.Pass()); }

        List<OsmMember> list = [.. relation.Members];
        OsmMember item = list[request.FromIndex];
        list.RemoveAt(request.FromIndex);
        list.Insert(request.ToIndex, item);
        OsmRelation updated = relation with { Members = [.. list] };
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
