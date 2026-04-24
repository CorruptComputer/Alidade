namespace Alidade.Osm.Models.Editing;

/// <summary>
///   The three typed collections returned by a bbox fetch.
/// </summary>
/// <param name="Nodes">OSM nodes within the bounding box.</param>
/// <param name="Ways">OSM ways within the bounding box.</param>
/// <param name="Relations">OSM relations within the bounding box.</param>
public record FetchBboxResult(IList<OsmNode> Nodes, IList<OsmWay> Ways, IList<OsmRelation> Relations);
