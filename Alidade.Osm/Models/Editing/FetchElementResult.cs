namespace Alidade.Osm.Models.Editing;

/// <summary>
///   The typed collections returned by a single-element fetch.
/// </summary>
/// <param name="Nodes">OSM nodes returned by the fetch (may include constituent nodes for ways).</param>
/// <param name="Ways">OSM ways returned by the fetch.</param>
/// <param name="Relations">OSM relations returned by the fetch.</param>
public record FetchElementResult(IList<OsmNode> Nodes, IList<OsmWay> Ways, IList<OsmRelation> Relations);
