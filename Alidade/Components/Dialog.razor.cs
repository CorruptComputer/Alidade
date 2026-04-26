using Alidade.Components.Dialogs;
using Microsoft.AspNetCore.Components;

namespace Alidade.Components;

/// <summary>
///   Generic dialog shell that owns the backdrop, container, and header chrome
///   (title and optional close button) for every modal dialog in the editor.
///   The <typeparamref name="TDialog"/> constraint supplies the default title via
///   <see cref="IDialog.Title"/>; pass <see cref="Title"/> to override it with a dynamic value.
/// </summary>
/// <typeparam name="TDialog">The dialog component type, used to read the static title.</typeparam>
public partial class Dialog<TDialog> where TDialog : IDialog
{
    /// <summary>
    ///   When <see langword="false"/>, the close button is hidden and backdrop clicks are ignored,
    ///   forcing the user to make an explicit choice via the dialog's action buttons.
    /// </summary>
    [Parameter]
    public bool CanClose { get; set; } = true;

    /// <summary>Invoked when the close button or dismissible backdrop is clicked.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Main body content, rendered between the header and the actions footer.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///   Button content rendered inside a <c>dialog-actions</c> div at the bottom of the dialog.
    ///   Omit for dialogs that manage their own inline actions across multiple states.
    /// </summary>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    /// <summary>
    ///   Additional CSS class(es) added to the dialog container div. Use for per-dialog
    ///   size overrides (e.g. <c>"changeset-dialog"</c>, <c>"conflict-dialog"</c>).
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    ///   Overrides <typeparamref name="TDialog"/>'s static <see cref="IDialog.Title"/> with a
    ///   dynamic value. Use for dialogs whose header text changes based on component state.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    private string DisplayTitle => Title ?? TDialog.Title;

    private void HandleBackdropClick()
    {
        if (CanClose)
        {
            _ = OnClose.InvokeAsync();
        }
    }
}
