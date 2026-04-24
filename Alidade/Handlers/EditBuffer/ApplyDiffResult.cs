namespace Alidade.Handlers.EditBuffer;

/// <inheritdoc />
public class ApplyDiffResult(EditBufferStateService editBufferState, SettingsStateService settingsState, IMediator mediator)
    : INotificationHandler<ApplyDiffResult.Notification>
{
    /// <summary>
    ///   Remaps temporary IDs to permanent OSM IDs after upload and clears dirty state.
    ///   If an endpoint switch was pending and the buffer is now clean, auto-confirms it.
    /// </summary>
    public record Notification(DiffResult Diff, IReadOnlyList<OsmElementRef> UploadedRefs) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        EditBufferState state = editBufferState.State;
        ImmutableDictionary<long, OsmNode> nodes = state.Nodes;
        ImmutableDictionary<long, OsmWay> ways = state.Ways;
        ImmutableDictionary<long, OsmRelation> relations = state.Relations;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        foreach ((long oldId, long newId) in notification.Diff.NodeIdMap)
        {
            if (!nodes.TryGetValue(oldId, out OsmNode? node)) { continue; }
            OsmNode updated = node with { Id = newId };
            nodes = nodes.Remove(oldId).SetItem(newId, updated);
            editStates = editStates
                .Remove(new OsmElementRef(OsmElementTypes.Node, oldId))
                .SetItem(new OsmElementRef(OsmElementTypes.Node, newId), EditState.Fetched);
        }

        foreach (OsmElementRef uploaded in notification.UploadedRefs)
        {
            editStates = editStates.Remove(uploaded);
        }

        editBufferState.SetState(state with { Nodes = nodes, Ways = ways, Relations = relations, EditStates = editStates });

        // Auto-confirm pending endpoint switch after a clean upload.
        // ConfirmEndpointSwitch handles the auth transition for the new endpoint.
        if (settingsState.PendingEndpoint is { } pending
            && !editBufferState.State.IsDirty)
        {
            await mediator.Send(new Settings.ConfirmEndpointSwitch.Command(pending), cancellationToken);
        }
    }
}
