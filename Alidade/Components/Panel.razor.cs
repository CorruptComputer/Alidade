using Alidade.Components.Panels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Alidade.Components;

/// <summary>
///   Generic panel shell that owns the drag handle, resize behaviour, and header chrome
///   (title, optional pin button, close button) for every floating panel in the editor.
///   The <typeparamref name="TPanel"/> constraint supplies the default title via
///   <see cref="IPanel.Title"/>; pass <see cref="Title"/> to override it with a dynamic value.
/// </summary>
/// <typeparam name="TPanel">The panel component type, used to read the static title.</typeparam>
/// <param name="js">The JS runtime used to register drag behaviour.</param>
public partial class Panel<TPanel>(IJSRuntime js) where TPanel : IPanel
{
    /// <summary>
    ///   When <see langword="true"/>, a pin button is shown in the header (only when
    ///   <see cref="PinnedRef"/> is <see langword="null"/>).
    /// </summary>
    [Parameter]
    public bool CanPin { get; set; }

    /// <summary>
    ///   When non-null the panel is in pinned state: the pin icon is shown and the close
    ///   button label changes to "Unpin".
    /// </summary>
    [Parameter]
    public OsmElementRef? PinnedRef { get; set; }

    /// <summary>Invoked when the close or unpin button is clicked.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Invoked when the pin button is clicked.</summary>
    [Parameter]
    public EventCallback OnPin { get; set; }

    /// <summary>The scrollable panel body content, wrapped in a <c>panel-body</c> div.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    ///   Optional extra content rendered inside the header, after the title. Used for
    ///   panels that need additional header widgets (e.g. a loading spinner).
    /// </summary>
    [Parameter]
    public RenderFragment? TitleAddon { get; set; }

    /// <summary>
    ///   Optional content rendered between the header and <c>panel-body</c>. Use for
    ///   fixed-height controls (e.g. a search input) that should not scroll with the body.
    /// </summary>
    [Parameter]
    public RenderFragment? PreBody { get; set; }

    /// <summary>
    ///   Optional content rendered below <c>panel-body</c>. Use for a <c>panel-footer</c>
    ///   that must remain visible regardless of body scroll position.
    /// </summary>
    [Parameter]
    public RenderFragment? Footer { get; set; }

    /// <summary>
    ///   Controls panel visibility via CSS <c>display: none</c> without unmounting the
    ///   component, preserving the drag reference and panel position.
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    ///   Additional CSS class(es) added to the root element. Use <c>panel--left</c> for
    ///   panels that should open on the left side, or <c>panel--pinned</c> for the gold
    ///   pinned-inspector variant.
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    ///   Overrides <typeparamref name="TPanel"/>'s static <see cref="IPanel.Title"/> with a
    ///   dynamic value. Use for panels whose header text changes based on selection state.
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    ///   When set, fires when the user right-clicks the panel header. The browser default context
    ///   menu is always suppressed on panel headers regardless of whether this is assigned.
    /// </summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnHeaderContextMenu { get; set; }

    private async Task HandleHeaderContextMenu(MouseEventArgs e)
    {
        if (OnHeaderContextMenu.HasDelegate)
        {
            await OnHeaderContextMenu.InvokeAsync(e);
        }
    }

    private string DisplayTitle => Title ?? TPanel.Title;

    private ElementReference _panelEl;
    private ElementReference _headerEl;
    private bool _dragInitialized;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_dragInitialized)
        {
            try
            {
                await js.InvokeVoidAsync("makePanelDraggable", _panelEl, _headerEl);
                _dragInitialized = true;
            }
            catch
            {
                // JS not ready yet; will retry on next render.
            }
        }
    }
}
