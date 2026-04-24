namespace Alidade.Osm.Models.Parsing;

/// <summary>
///   The typed element collections parsed from a raw OSM XML map response.
/// </summary>
/// <param name="Nodes">OSM nodes parsed from the response.</param>
/// <param name="Ways">OSM ways parsed from the response.</param>
/// <param name="Relations">OSM relations parsed from the response.</param>
public record ParseOsmXmlResult(IList<OsmNode> Nodes, IList<OsmWay> Ways, IList<OsmRelation> Relations);
