namespace Alidade.Map.Models;

/// <summary>
///   Data carried by a click event forwarded from the MapLibre canvas to the Blazor component tree.
/// </summary>
/// <param name="Lat">Geographic latitude of the click in decimal degrees.</param>
/// <param name="Lon">Geographic longitude of the click in decimal degrees.</param>
/// <param name="ScreenX">Horizontal CSS pixel offset from the left edge of the map container.</param>
/// <param name="ScreenY">Vertical CSS pixel offset from the top edge of the map container.</param>
/// <param name="ElementId">
///   The feature ID of the topmost OSM element under the click (e.g. <c>"node/12345"</c>),
///   or null if the click landed on empty map space.
/// </param>
/// <param name="ShiftKey">Whether the Shift key was held at the time of the click (used for multi-select).</param>
public record MapClickEvent(double Lat, double Lon, double ScreenX, double ScreenY, string? ElementId, bool ShiftKey = false);
