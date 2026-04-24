namespace Alidade.Osm.Models;

/// <summary>
///   The output of <see cref="Alidade.Osm.Services.GeometryService.Gridify"/>: positions for new nodes to create,
///   and per-cell closed rings that reference either new or existing (reused) nodes.
/// </summary>
public record GridifyResult(
    IReadOnlyList<(double Lat, double Lon)> NewNodes,
    IReadOnlyList<IReadOnlyList<GridifyNodeRef>> CellNodeRefs)
{
    /// <summary>
    ///   An empty result indicating the operation cannot be applied.
    /// </summary>
    public static readonly GridifyResult Empty = new([], []);
}
