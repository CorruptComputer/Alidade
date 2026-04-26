using Alidade.Osm.Handlers.Tools.Square;
using Alidade.Osm.Models.Tools.Square;

namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public sealed class Square(SelectionStateService selectionState, IMediator mediator)
    : INotificationHandler<Square.Notification>
{
    /// <summary>
    ///   Squares all selected ways and relations, moving corners toward the nearest 90° increment.
    ///   Ways are squared individually. Relations are squared collectively so all member ways
    ///   converge to shared orthogonal axes rather than each way finding its own independent axes.
    /// </summary>
    public record Notification : INotification;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        ImmutableHashSet<OsmElementRef> selected = selectionState.State.Selected;

        foreach (OsmElementRef wayRef in selected.Where(r => r.Type == OsmElementTypes.Way))
        {
            SquareResult? result = (await mediator.Send(new SquareWay.Query(wayRef.Id), cancellationToken)).Result;
            if (result is { Moves.Count: > 0 })
            {
                await mediator.Send(new CommitSquareWay.Command(result), cancellationToken);
            }
        }

        foreach (OsmElementRef relRef in selected.Where(r => r.Type == OsmElementTypes.Relation))
        {
            SquareResult? result = (await mediator.Send(new SquareRelation.Query(relRef.Id), cancellationToken)).Result;
            if (result is { Moves.Count: > 0 })
            {
                await mediator.Send(new CommitSquareRelation.Command(result), cancellationToken);
            }
        }
    }
}
