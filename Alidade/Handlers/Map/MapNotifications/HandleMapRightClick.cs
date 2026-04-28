using Alidade.Handlers.Map;
using Alidade.Map.Notifications;

namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public sealed class HandleMapRightClick(
    SelectionStateService selectionState,
    IMediator mediator)
    : INotificationHandler<MapRightClicked.Notification>
{
    /// <inheritdoc />
    public async Task Handle(MapRightClicked.Notification notification, CancellationToken cancellationToken)
    {
        OsmElementRef? target = ResolveTarget(notification.ElementIds);

        if (target is null)
        {
            return;
        }

        await mediator.Send(
            new ShowContextMenu.Command(notification.ScreenX, notification.ScreenY, target),
            cancellationToken);
    }

    private OsmElementRef? ResolveTarget(string[] elementIds)
    {
        // Prefer whichever candidate is currently selected (handles overlapping ways where the
        // selected one is beneath the top-most paint-order feature).
        ImmutableHashSet<OsmElementRef> selected = selectionState.State.Selected;
        OsmElementRef? first = null;

        foreach (string elementId in elementIds)
        {
            OsmElementRef? candidate = ParseElementId(elementId);
            if (candidate is null)
            {
                continue;
            }

            first ??= candidate;

            if (selected.Contains(candidate))
            {
                return candidate;
            }
        }

        // No selected candidate — use the top feature, or fall back to the current selection.
        return first ?? selectionState.State.SingleSelected;
    }

    private static OsmElementRef? ParseElementId(string elementId)
    {
        if (elementId.StartsWith("way/") && long.TryParse(elementId[4..], out long wayId))
        {
            return new OsmElementRef(OsmElementTypes.Way, wayId);
        }

        if (elementId.StartsWith("node/") && long.TryParse(elementId[5..], out long nodeId))
        {
            return new OsmElementRef(OsmElementTypes.Node, nodeId);
        }

        if (elementId.StartsWith("relation/") && long.TryParse(elementId[9..], out long relationId))
        {
            return new OsmElementRef(OsmElementTypes.Relation, relationId);
        }

        return null;
    }
}
