using Alidade.Osm.Handlers.Editing;

namespace Alidade.Handlers.Selection;

/// <inheritdoc />
public class DeleteSelected(SelectionStateService selectionState, IMediator mediator) : IRequestHandler<DeleteSelected.Command, CommandResult>
{
    /// <summary>
    ///   Dispatches a delete command for each selected element, then clears the selection.
    /// </summary>
    public record Command(IReadOnlyCollection<OsmElementRef> Elements) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        foreach (OsmElementRef elem in request.Elements)
        {
            switch (elem.Type)
            {
                case OsmElementTypes.Node:
                    await mediator.Send(new DeleteNode.Command(elem.Id), cancellationToken);
                    break;
                case OsmElementTypes.Way:
                    await mediator.Send(new DeleteWay.Command(elem.Id), cancellationToken);
                    break;
                case OsmElementTypes.Relation:
                    await mediator.Send(new DeleteRelation.Command(elem.Id), cancellationToken);
                    break;
            }
        }

        selectionState.SetState(selectionState.State with { Selected = [] });
        return CommandResult.Pass();
    }
}
