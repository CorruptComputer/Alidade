namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class Circularize(SelectionStateService selectionState, EditBufferStateService editBuffer, IMediator mediator)
    : IRequestHandler<Circularize.Command, CommandResult>
{
    /// <summary>
    ///   Fits all selected closed ways to a best-fit circle.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        foreach (OsmElementRef wayRef in selectionState.State.Selected.Where(r => r.Type == OsmElementTypes.Way))
        {
            IReadOnlyList<(long, double, double, double, double)> moves =
                GeometryService.Circularize(wayRef.Id, editBuffer.State.Ways, editBuffer.State.Nodes);
            if (moves.Count > 0)
            {
                await mediator.Send(new CircularizeNodes.Command(moves), cancellationToken);
            }
        }

        return CommandResult.Pass();
    }
}
