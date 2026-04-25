namespace Alidade.Osm.Models;

/// <summary>
///   The three typed OSM element collections stored in and returned by
///   <see cref="IOsmCacheService"/>. Contains only data fetched
///   from the OSM API this session — never locally-edited elements.
/// </summary>
/// <param name="Nodes">OSM nodes included in the cached area.</param>
/// <param name="Ways">OSM ways included in the cached area.</param>
/// <param name="Relations">OSM relations included in the cached area.</param>
public record OsmCacheData(IReadOnlyList<OsmNode> Nodes, IReadOnlyList<OsmWay> Ways, IReadOnlyList<OsmRelation> Relations);
