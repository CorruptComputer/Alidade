namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class RestoreDraft(EditBufferStateService editBufferState) : IRequestHandler<RestoreDraft.Command, CommandResult>
{
    /// <summary>
    ///   Merges a saved draft back into the edit buffer.
    /// </summary>
    public record Command(EditBufferDraft Draft) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<long, OsmNode> nodes = state.Nodes;
        ImmutableDictionary<long, OsmWay> ways = state.Ways;
        ImmutableDictionary<long, OsmRelation> relations = state.Relations;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        foreach (DraftNode dn in request.Draft.Nodes)
        {
            OsmNode node = new(dn.Id, dn.Version, dn.ChangesetId, null, null,
                dn.Lat, dn.Lon, dn.Tags.ToImmutableDictionary());
            nodes = nodes.SetItem(dn.Id, node);
        }

        foreach (DraftWay dw in request.Draft.Ways)
        {
            OsmWay way = new(dw.Id, dw.Version, dw.ChangesetId, null, null,
                dw.NodeIds, dw.Tags.ToImmutableDictionary());
            ways = ways.SetItem(dw.Id, way);
        }

        foreach (DraftRelation dr in request.Draft.Relations)
        {
            IReadOnlyList<OsmMember> members = dr.Members
                .Select(m => new OsmMember((OsmElementTypes)m.Type, m.Ref, m.Role))
                .ToArray();
            OsmRelation relation = new(dr.Id, dr.Version, dr.ChangesetId, null, null,
                members, dr.Tags.ToImmutableDictionary());
            relations = relations.SetItem(dr.Id, relation);
        }

        foreach (DraftEditState des in request.Draft.EditStates)
        {
            OsmElementRef elemRef = new((OsmElementTypes)des.Type, des.Id);
            editStates = editStates.SetItem(elemRef, (EditState)des.State);
        }

        editBufferState.SetState(state with
        {
            Nodes = nodes,
            Ways = ways,
            Relations = relations,
            EditStates = editStates,
            NextNegativeId = request.Draft.NextNegativeId
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
