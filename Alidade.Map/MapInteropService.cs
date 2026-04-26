using Microsoft.JSInterop;

namespace Alidade.Map;

/// <summary>
///   Abstraction over the MapLibre GL JS map instance exposed via <c>window.mapInterop</c>.
///   Provides async methods for controlling the map and publishes Questy notifications when
///   the user interacts with the canvas.
/// </summary>
/// <param name="js">The JS runtime used for all interop calls.</param>
/// <param name="mediator">The mediator used to publish map interaction notifications.</param>
public sealed class MapInteropService(IJSRuntime js, IMediator mediator) : IAsyncDisposable
{
    private DotNetObjectReference<MapInteropService>? _dotnetRef;
    private readonly Dictionary<string, string> _lastSourceData = [];

    /// <summary>
    ///   Initializes the MapLibre map inside the specified container and registers this
    ///   instance as the JS callback target.
    /// </summary>
    /// <param name="containerId">The HTML element ID of the map container.</param>
    public async ValueTask InitializeAsync(string containerId)
    {
        _dotnetRef = DotNetObjectReference.Create(this);
        _lastSourceData.Clear();
        await js.InvokeVoidAsync("mapInterop.initialize", containerId, null, _dotnetRef);
    }

    /// <summary>
    ///   Replaces the GeoJSON data for the named MapLibre source. Skips the JS interop call
    ///   when the serialized payload is identical to what was last sent for that source.
    /// </summary>
    /// <param name="sourceId">The MapLibre source ID (e.g. <c>"osm-nodes"</c>).</param>
    /// <param name="geojsonJson">A serialized GeoJSON FeatureCollection string.</param>
    public ValueTask SetSourceDataAsync(string sourceId, string geojsonJson)
    {
        if (_lastSourceData.TryGetValue(sourceId, out string? prev) && prev == geojsonJson)
        {
            return ValueTask.CompletedTask;
        }

        _lastSourceData[sourceId] = geojsonJson;
        return js.InvokeVoidAsync("mapInterop.setSourceData", sourceId, geojsonJson);
    }

    /// <summary>
    ///   Animates the viewport to the given coordinates at the specified zoom level.
    /// </summary>
    /// <param name="lat">Target latitude in decimal degrees.</param>
    /// <param name="lon">Target longitude in decimal degrees.</param>
    /// <param name="zoom">Target MapLibre zoom level.</param>
    public ValueTask FlyToAsync(double lat, double lon, int zoom)
        => js.InvokeVoidAsync("mapInterop.flyTo", lat, lon, zoom);

    /// <summary>
    ///   Pans the viewport to the given coordinates without changing the zoom level.
    /// </summary>
    /// <param name="lat">Target latitude in decimal degrees.</param>
    /// <param name="lon">Target longitude in decimal degrees.</param>
    public ValueTask PanToAsync(double lat, double lon)
        => js.InvokeVoidAsync("mapInterop.panTo", lat, lon);

    /// <summary>
    ///   Returns the current viewport bounds, or <c>null</c> if the map is not yet initialized.
    /// </summary>
    /// <returns>The current bounds and zoom, or <c>null</c>.</returns>
    public ValueTask<MapBounds?> GetBoundsAsync()
        => js.InvokeAsync<MapBounds?>("mapInterop.getBounds");

    /// <summary>
    ///   Projects geographic coordinates to a CSS pixel point on the map canvas.
    /// </summary>
    /// <param name="lat">Latitude in decimal degrees.</param>
    /// <param name="lon">Longitude in decimal degrees.</param>
    /// <returns>The screen-space pixel coordinate.</returns>
    public ValueTask<ScreenPoint> ProjectAsync(double lat, double lon)
        => js.InvokeAsync<ScreenPoint>("mapInterop.project", lat, lon);

    /// <summary>
    ///   Sets the active editing tool in MapLibre, controlling cursor and interaction behaviour.
    /// </summary>
    /// <param name="toolName">
    ///   The tool name string expected by <c>mapInterop.setActiveTool</c>
    ///   (e.g. <c>"select"</c>, <c>"drawNode"</c>).
    /// </param>
    public ValueTask SetActiveToolAsync(string toolName)
        => js.InvokeVoidAsync("mapInterop.setActiveTool", toolName);

    /// <summary>
    ///   Shows or hides the named MapLibre layer.
    /// </summary>
    /// <param name="layerId">The MapLibre layer ID.</param>
    /// <param name="visible">Whether the layer should be visible.</param>
    public ValueTask SetLayerVisibilityAsync(string layerId, bool visible)
        => js.InvokeVoidAsync("mapInterop.setLayerVisibility", layerId, visible);

    /// <summary>
    ///   Switches the background imagery to a TMS or XYZ tile provider.
    /// </summary>
    /// <param name="tiles">One or more tile URL templates.</param>
    /// <param name="tileSize">Tile size in pixels (typically 256 or 512).</param>
    /// <param name="attribution">Attribution text, or <c>null</c> for none.</param>
    /// <param name="tmsScheme">Whether the URL uses TMS (inverted Y) tile addressing.</param>
    /// <param name="maxZoom">Optional maximum zoom level for the source.</param>
    public ValueTask SetBackgroundImageryAsync(string[] tiles, int tileSize, string? attribution, bool tmsScheme, int? maxZoom = null)
        => js.InvokeVoidAsync("mapInterop.setBackgroundImagery", tiles, tileSize, attribution ?? string.Empty, tmsScheme, maxZoom);

    /// <summary>
    ///   Switches the background imagery to Bing Maps aerial.
    /// </summary>
    public ValueTask SetBackgroundBingAsync()
        => js.InvokeVoidAsync("mapInterop.setBackgroundBing");

    /// <summary>
    ///   Removes the current background imagery layer.
    /// </summary>
    public ValueTask ClearBackgroundImageryAsync()
        => js.InvokeVoidAsync("mapInterop.clearBackgroundImagery");

    /// <summary>
    ///   Called from JS when the user clicks the map canvas.
    /// </summary>
    /// <param name="lat">Click latitude.</param>
    /// <param name="lon">Click longitude.</param>
    /// <param name="screenX">Click X position in CSS pixels.</param>
    /// <param name="screenY">Click Y position in CSS pixels.</param>
    /// <param name="elementId">The feature ID string under the click, if any.</param>
    /// <param name="addToSelection">Whether a modifier key (Shift, Ctrl, or Cmd) was held during the click.</param>
    [JSInvokable]
    public void OnMapClick(double lat, double lon, double screenX, double screenY, string? elementId, bool addToSelection = false)
        => _ = mediator.Publish(new MapClicked.Notification(new MapClickEvent(lat, lon, screenX, screenY, elementId, addToSelection)));

    /// <summary>
    ///   Called from JS when the user double-clicks the map canvas on an OSM feature.
    /// </summary>
    /// <param name="lat">Click latitude.</param>
    /// <param name="lon">Click longitude.</param>
    /// <param name="screenX">Click X position in CSS pixels.</param>
    /// <param name="screenY">Click Y position in CSS pixels.</param>
    /// <param name="elementId">The feature ID string under the click, if any.</param>
    [JSInvokable]
    public void OnMapDblClick(double lat, double lon, double screenX, double screenY, string? elementId)
        => _ = mediator.Publish(new MapDblClicked.Notification(new MapClickEvent(lat, lon, screenX, screenY, elementId, false)));

    /// <summary>
    ///   Called from JS after a pan or zoom gesture ends.
    /// </summary>
    /// <param name="west">Western longitude bound.</param>
    /// <param name="south">Southern latitude bound.</param>
    /// <param name="east">Eastern longitude bound.</param>
    /// <param name="north">Northern latitude bound.</param>
    /// <param name="zoom">Current zoom level.</param>
    [JSInvokable]
    public void OnMapMoveEnd(double west, double south, double east, double north, double zoom)
        => _ = mediator.Publish(new MapMoveEnded.Notification(new MapBounds(west, south, east, north, zoom)));

    /// <summary>
    ///   Called from JS when the pointer moves over a new feature or leaves all features.
    /// </summary>
    /// <param name="elementId">The feature ID string, or <c>null</c> when leaving.</param>
    [JSInvokable]
    public void OnHover(string? elementId)
        => _ = mediator.Publish(new HoverChanged.Notification(elementId));

    /// <summary>
    ///   Called from JS when the user releases a node drag handle.
    /// </summary>
    /// <param name="elementId">The feature ID of the dragged node (e.g. <c>node/123</c>).</param>
    /// <param name="lat">The new latitude after the drag.</param>
    /// <param name="lon">The new longitude after the drag.</param>
    /// <param name="snapTargetId">Node feature ID under the cursor at drop time, or <c>null</c>.</param>
    /// <param name="waySnapTargetId">Way feature ID under the cursor at drop time, or <c>null</c>.</param>
    /// <param name="waySnapSegmentNodeA">Node ID string of one endpoint of the nearest snap segment, or <c>null</c>.</param>
    /// <param name="waySnapSegmentNodeB">Node ID string of the other endpoint of the nearest snap segment, or <c>null</c>.</param>
    [JSInvokable]
    public void OnNodeDragEnd(string elementId, double lat, double lon, string? snapTargetId = null, string? waySnapTargetId = null, string? waySnapSegmentNodeA = null, string? waySnapSegmentNodeB = null)
        => _ = mediator.Publish(new NodeDragEnded.Notification(new NodeDragEvent(elementId, lat, lon, snapTargetId, waySnapTargetId, waySnapSegmentNodeA, waySnapSegmentNodeB)));

    /// <summary>
    ///   Called from JS when the user presses a key outside a text input.
    ///   The combo is pre-normalized by JS (e.g. <c>"ctrl+z"</c>, <c>"delete"</c>).
    /// </summary>
    /// <param name="combo">The normalized key combo string.</param>
    [JSInvokable]
    public void OnKeyDown(string combo)
        => _ = mediator.Publish(new KeyPressed.Notification(combo));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _dotnetRef?.Dispose();
        await ValueTask.CompletedTask;
    }
}
