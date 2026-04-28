using Alidade.Osm.Handlers.Tools.Circularize;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models.Tools.Circularize;

/// <summary>
///   The result of a circularize geometry computation — node moves and optionally new nodes
///   to insert, to commit via <see cref="CommitCircularize"/>.
///   Coordinates are in WGS-84 (X = longitude, Y = latitude).
/// </summary>
/// <param name="Moves">
///   Node position changes. Each entry carries the node ID, the original WGS-84 coordinate
///   (or <see langword="null"/> for newly-created nodes), and the corrected WGS-84 coordinate.
/// </param>
/// <param name="FinalWayNodeList">
///   The complete ordered node list for the updated way, referencing existing nodes by ID
///   and new nodes by their index into the new-node subset of <see cref="Moves"/>.
///   Empty when no way update is needed (i.e. no new nodes were added).
/// </param>
public record CircularizeResult(
    IReadOnlyList<(long NodeId, Coordinate? Old, Coordinate New)> Moves,
    IReadOnlyList<CircularizeNodeRef> FinalWayNodeList)
{
    /// <summary>
    ///   An empty result indicating no corrections are needed.
    /// </summary>
    public static readonly CircularizeResult Empty = new([], []);
}
