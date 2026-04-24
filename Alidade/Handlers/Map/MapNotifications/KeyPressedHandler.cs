using Alidade.Handlers.Selection;
using Alidade.Handlers.Tool;
using Alidade.Handlers.UndoRedo;
using Alidade.Handlers.Validation;

namespace Alidade.Handlers.Map.MapNotifications;

/// <inheritdoc />
public sealed class KeyPressedHandler(SettingsStateService settingsState, SelectionStateService selectionState, ISender sender)
    : INotificationHandler<KeyPressed.Notification>
{
    /// <inheritdoc />
    public async Task Handle(KeyPressed.Notification notification, CancellationToken cancellationToken)
    {
        string? actionId = settingsState.State.KeyBindings.FindAction(notification.Combo);

        if (actionId is null)
        {
            return;
        }

        switch (actionId)
        {
            case KeyBindingActions.SelectTool:
                await sender.Send(new SetActiveTool.Command(ActiveTools.Select), cancellationToken);
                break;
            case KeyBindingActions.DrawNode:
                await sender.Send(new SetActiveTool.Command(ActiveTools.DrawNode), cancellationToken);
                break;
            case KeyBindingActions.DrawWay:
                await sender.Send(new SetActiveTool.Command(ActiveTools.DrawWay), cancellationToken);
                break;
            case KeyBindingActions.DrawArea:
                await sender.Send(new SetActiveTool.Command(ActiveTools.DrawArea), cancellationToken);
                break;
            case KeyBindingActions.CancelTool:
                await sender.Send(new EndDrawing.Command(), cancellationToken);
                break;
            case KeyBindingActions.Undo:
                await sender.Send(new Undo.Command(), cancellationToken);
                break;
            case KeyBindingActions.Redo:
                await sender.Send(new Redo.Command(), cancellationToken);
                break;
            case KeyBindingActions.DeleteSelected:
                await sender.Send(new DeleteSelected.Command(selectionState.State.Selected), cancellationToken);
                break;
            case KeyBindingActions.Orthogonalize:
                await sender.Send(new Orthogonalize.Command(), cancellationToken);
                break;
            case KeyBindingActions.Circularize:
                await sender.Send(new Circularize.Command(), cancellationToken);
                break;
            case KeyBindingActions.Upload:
                await sender.Send(new ToggleUploadDialog.Command(), cancellationToken);
                break;
            case KeyBindingActions.Gridify:
                await sender.Send(new ToggleGridifyDialog.Command(), cancellationToken);
                break;
            case KeyBindingActions.ToggleBackground:
                await sender.Send(new ToggleBackgroundPanel.Command(), cancellationToken);
                break;
            case KeyBindingActions.ToggleSettings:
                await sender.Send(new ToggleSettingsPanel.Command(), cancellationToken);
                break;
            case KeyBindingActions.ToggleValidation:
                await sender.Send(new ToggleValidationPanel.Command(), cancellationToken);
                break;
            case KeyBindingActions.ToggleUndoHistory:
                await sender.Send(new ToggleUndoHistory.Command(), cancellationToken);
                break;
        }
    }
}
