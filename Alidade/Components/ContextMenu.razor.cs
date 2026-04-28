using Alidade.Handlers.Map;
using Alidade.Handlers.Selection;
using Alidade.Handlers.Tool;

namespace Alidade.Components;

/// <summary>
///   Right-click context menu offering Square, Circularize, and Gridify for the target element.
///   Positioned at the coordinates stored in <see cref="MapStateService"/> and dismissed by
///   clicking the backdrop or selecting an action.
/// </summary>
public partial class ContextMenu(
    MapStateService mapState,
    SelectionStateService selectionState,
    IMediator mediator) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
        => mapState.StateChanged += OnStateChanged;

    private void OnStateChanged(object? sender, EventArgs e)
        => StateHasChanged();

    private async Task OnSquare()
    {
        await EnsureTargetSelected();
        await mediator.Publish(new Square.Notification());
        await mediator.Send(new HideContextMenu.Command());
    }

    private async Task OnCircularize()
    {
        await EnsureTargetSelected();
        await mediator.Send(new ToggleCircularizeDialog.Command());
        await mediator.Send(new HideContextMenu.Command());
    }

    private async Task OnGridify()
    {
        await EnsureTargetSelected();
        await mediator.Send(new ToggleGridifyDialog.Command());
        await mediator.Send(new HideContextMenu.Command());
    }

    private async Task OnBackdropClick()
        => await mediator.Send(new HideContextMenu.Command());

    private async Task EnsureTargetSelected()
    {
        OsmElementRef? target = mapState.State.ContextMenuTargetElement;
        if (target is not null && !selectionState.State.Selected.Contains(target))
        {
            await mediator.Send(new Select.Command(target, false));
        }
    }

    /// <inheritdoc />
    public void Dispose()
        => mapState.StateChanged -= OnStateChanged;
}
