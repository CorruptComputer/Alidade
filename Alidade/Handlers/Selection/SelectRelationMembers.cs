namespace Alidade.Handlers.Selection;

/// <inheritdoc />
public class SelectRelationMembers(EditBufferStateService editBufferState, SelectionStateService selectionState)
    : IRequestHandler<SelectRelationMembers.Command, CommandResult>
{
    /// <summary>
    ///   Selects all downloaded members of a relation, replacing the current selection.
    ///   Members whose referenced elements are not present in the edit buffer are silently skipped.
    /// </summary>
    /// <param name="RelationId">The ID of the relation whose members should be selected.</param>
    public record Command(long RelationId) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState buffer = editBufferState.State;
        if (!buffer.Relations.TryGetValue(request.RelationId, out OsmRelation? relation))
        {
            return Task.FromResult(CommandResult.Pass());
        }

        ImmutableHashSet<OsmElementRef>.Builder builder = ImmutableHashSet.CreateBuilder<OsmElementRef>();
        foreach (OsmMember member in relation.Members)
        {
            bool downloaded = member.Type switch
            {
                OsmElementTypes.Node => buffer.Nodes.ContainsKey(member.Ref),
                OsmElementTypes.Way => buffer.Ways.ContainsKey(member.Ref),
                OsmElementTypes.Relation => buffer.Relations.ContainsKey(member.Ref),
                _ => false
            };
            if (downloaded)
            {
                builder.Add(new OsmElementRef(member.Type, member.Ref));
            }
        }

        selectionState.SetState(selectionState.State with { Selected = builder.ToImmutable() });
        return Task.FromResult(CommandResult.Pass());
    }
}
