namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class Orthogonalize(SelectionStateService selectionState, EditBufferStateService editBuffer, IMediator mediator)
    : IRequestHandler<Orthogonalize.Command, CommandResult>
{
    /// <summary>
    ///   Orthogonalizes all selected ways, squaring their corners toward 90°.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        foreach (OsmElementRef wayRef in selectionState.State.Selected.Where(r => r.Type == OsmElementTypes.Way))
        {
            IReadOnlyList<(long, double, double, double, double)> moves =
                GeometryService.Orthogonalize(wayRef.Id, editBuffer.State.Ways, editBuffer.State.Nodes);
            if (moves.Count > 0)
            {
                await mediator.Send(new OrthogonalizeNodes.Command(moves), cancellationToken);
            }
        }

        return CommandResult.Pass();
    }
}
