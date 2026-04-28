using NetTopologySuite.Geometries;

namespace Alidade.Core.Models;

/// <summary>
///   An axis-aligned geographic bounding box defined by two corner coordinates.
/// </summary>
/// <param name="NorthWest">The north-west corner (max latitude, min longitude).</param>
/// <param name="SouthEast">The south-east corner (min latitude, max longitude).</param>
public record Bbox(Coordinate NorthWest, Coordinate SouthEast);
