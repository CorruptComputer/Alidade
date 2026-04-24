namespace Alidade.Handlers.Selection;

/// <inheritdoc />
public class SetHovered(SelectionStateService selectionState) : IRequestHandler<SetHovered.Command, CommandResult>
{
    /// <summary>
    ///   Updates the hovered element reference.
    /// </summary>
    public record Command(OsmElementRef? Element) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        selectionState.SetState(selectionState.State with { Hovered = request.Element });
        return Task.FromResult(CommandResult.Pass());
    }
}
