namespace Alidade.Osm.Models;

/// <summary>
///   An axis-aligned geographic bounding box used by <see cref="IOsmCacheService"/>.
///   Equivalent to <c>Alidade.Map.Models.MapBounds</c> without the zoom level, since the
///   cache layer operates purely on geographic extent.
/// </summary>
/// <param name="West">Western longitude bound in decimal degrees.</param>
/// <param name="South">Southern latitude bound in decimal degrees.</param>
/// <param name="East">Eastern longitude bound in decimal degrees.</param>
/// <param name="North">Northern latitude bound in decimal degrees.</param>
public record CacheBounds(double West, double South, double East, double North);
