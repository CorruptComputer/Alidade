namespace Alidade.Handlers.Selection;

/// <inheritdoc />
public class Select(SelectionStateService selectionState) : IRequestHandler<Select.Command, CommandResult>
{
    /// <summary>
    ///   Handles element selection, supporting single-select and shift-click multi-select.
    /// </summary>
    public record Command(OsmElementRef? Element, bool AddToSelection) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        if (request.Element is null)
        {
            selectionState.SetState(selectionState.State with { Selected = [] });
            return Task.FromResult(CommandResult.Pass());
        }

        ImmutableHashSet<OsmElementRef> selected;
        if (request.AddToSelection)
        {
            selected = selectionState.State.Selected.Contains(request.Element)
                ? selectionState.State.Selected.Remove(request.Element)
                : selectionState.State.Selected.Add(request.Element);
        }
        else
        {
            selected = ImmutableHashSet.Create(request.Element);
        }

        selectionState.SetState(selectionState.State with { Selected = selected });
        return Task.FromResult(CommandResult.Pass());
    }
}
