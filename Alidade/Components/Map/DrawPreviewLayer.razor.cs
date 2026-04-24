namespace Alidade.Components.Map;

/// <summary>
///   Invisible component that subscribes to <see cref="ToolStateService"/> changes and triggers
///   a rubber-band preview push to the <c>osm-draw-preview</c> MapLibre source.
/// </summary>
public partial class DrawPreviewLayer(
    ToolStateService toolState,
    IMediator mediator) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
        => toolState.StateChanged += OnToolStateChanged;

    private void OnToolStateChanged(object? sender, EventArgs e)
        => _ = mediator.Publish(new Handlers.Map.DrawPreviewPushRequested.Notification());

    /// <inheritdoc />
    public void Dispose()
        => toolState.StateChanged -= OnToolStateChanged;
}
