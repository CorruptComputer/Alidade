namespace Alidade.Map.Models;

/// <summary>
///   The visible geographic bounds of the map viewport plus the current zoom level.
/// </summary>
/// <param name="West">Western longitude of the viewport in decimal degrees.</param>
/// <param name="South">Southern latitude of the viewport in decimal degrees.</param>
/// <param name="East">Eastern longitude of the viewport in decimal degrees.</param>
/// <param name="North">Northern latitude of the viewport in decimal degrees.</param>
/// <param name="Zoom">Current MapLibre zoom level.</param>
public record MapBounds(double West, double South, double East, double North, double Zoom);
