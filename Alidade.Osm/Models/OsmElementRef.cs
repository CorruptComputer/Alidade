namespace Alidade.Osm.Models;

/// <summary>
///   A lightweight discriminated identifier for an OSM element by type and ID.
/// </summary>
/// <param name="Type">The element's type.</param>
/// <param name="Id">The element's unique numeric ID.</param>
public record OsmElementRef(OsmElementTypes Type, long Id);
