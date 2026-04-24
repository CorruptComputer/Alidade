using Alidade.Map.Handlers;

namespace Alidade.Components.Map;

/// <summary>
///   Full-screen map canvas. Initializes MapLibre on first render.
/// </summary>
public partial class MapViewport(IMediator mediator)
{
    private bool _initialized;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized)
        {
            return;
        }

        _initialized = true;
        await mediator.Send(new InitializeMap.Command("map-container"));
    }
}
