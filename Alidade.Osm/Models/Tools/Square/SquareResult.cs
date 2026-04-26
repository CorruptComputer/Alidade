using Alidade.Osm.Handlers.Tools.Square;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models.Tools.Square;

/// <summary>
///   The result of a square geometry computation — a dictionary mapping node ID to corrected
///   position, to commit via <see cref="CommitSquareWay"/> or <see cref="CommitSquareRelation"/>.
///   Coordinates are in WGS-84 (X = longitude, Y = latitude).
/// </summary>
/// <param name="Moves">
///   Corrected node positions keyed by node ID, in WGS-84 (X = longitude, Y = latitude).
/// </param>
public record SquareResult(ImmutableDictionary<long, Coordinate> Moves)
{
    /// <summary>
    ///   An empty result indicating no corrections are needed.
    /// </summary>
    public static readonly SquareResult Empty = new([]);
}
