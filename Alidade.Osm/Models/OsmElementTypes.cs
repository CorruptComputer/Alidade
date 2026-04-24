namespace Alidade.Osm.Models;

/// <summary>
///   The geometric type of an OSM element.
/// </summary>
public enum OsmElementTypes
{
    /// <summary>
    ///   A point element defined by a single latitude/longitude coordinate.
    /// </summary>
    Node,

    /// <summary>
    ///   A linear or area element defined by an ordered sequence of nodes.
    /// </summary>
    Way,

    /// <summary>
    ///   A grouping element that relates multiple nodes, ways, or other relations.
    /// </summary>
    Relation
}
