namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CreateRelation(EditBufferStateService editBufferState) : IRequestHandler<CreateRelation.Command, CommandResult>
{
    static CreateRelation() => UndoDescriptions.Register<Command>("Create relation");

    /// <summary>
    ///   Creates a new relation with one initial member and the provided tags.
    /// </summary>
    /// <param name="FirstMember">The element to add as the first member.</param>
    /// <param name="MemberRole">The role of the first member within the relation.</param>
    /// <param name="Tags">Initial tag set for the relation.</param>
    public record Command(
        OsmElementRef FirstMember,
        string MemberRole,
        IReadOnlyDictionary<string, string> Tags)
        : IRequest<CommandResult>, IUndoableCommand;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        long id = state.NextNegativeId;
        OsmMember member = new(request.FirstMember.Type, request.FirstMember.Id, request.MemberRole);
        OsmRelation relation = new(id, 1, null, null, null, [member], request.Tags.ToImmutableDictionary());
        editBufferState.SetState(state with
        {
            Relations = state.Relations.SetItem(id, relation),
            EditStates = state.EditStates.SetItem(relation.Ref, EditState.Created),
            NextNegativeId = id - 1
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
