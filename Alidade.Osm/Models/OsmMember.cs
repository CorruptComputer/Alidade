namespace Alidade.Osm.Models;

/// <summary>
///   A single member of an OSM relation.
/// </summary>
/// <param name="Type">Whether the member is a node, way, or relation.</param>
/// <param name="Ref">The numeric ID of the referenced element.</param>
/// <param name="Role">
///   The semantic role this member plays within the relation (e.g. <c>"outer"</c>,
///   <c>"inner"</c>, <c>"stop"</c>, <c>"platform"</c>). An empty string means no role
///   is assigned.
/// </param>
public record OsmMember(OsmElementTypes Type, long Ref, string Role);
