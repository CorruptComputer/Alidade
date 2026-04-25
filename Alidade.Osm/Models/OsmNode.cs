using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models;

/// <summary>
///   An OSM node (point geometry) with tags.
/// </summary>
/// <param name="Id">
///   The node's numeric ID. Negative values are temporary placeholder IDs for locally created
///   nodes that have not yet been uploaded.
/// </param>
/// <param name="Version">
///   The server-assigned version number, incremented on each upload. <c>0</c> for locally
///   created nodes that have never been uploaded.
/// </param>
/// <param name="ChangesetId">The ID of the changeset that last modified this node, or null for locally created nodes.</param>
/// <param name="UserName">The OSM username of the last editor, or null if not available.</param>
/// <param name="Timestamp">The UTC time of the last server-side edit, or null for locally created nodes.</param>
/// <param name="Lat">Latitude in decimal degrees (WGS 84), in the range −90 to +90.</param>
/// <param name="Lon">Longitude in decimal degrees (WGS 84), in the range −180 to +180.</param>
/// <param name="Tags">Key-value pairs describing the node. Empty for geometry-only nodes (e.g. way vertices).</param>
public sealed record OsmNode(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTimeOffset? Timestamp,
    double Lat,
    double Lon,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>
    ///   Returns a typed reference to this node.
    /// </summary>
    public OsmElementRef Ref => new(OsmElementTypes.Node, Id);

    /// <summary>
    ///   Converts this to an NTS <see cref="Feature"/> with a Point geometry.
    ///   Tag key/value pairs are stored as attributes prefixed with <c>tag:</c>.
    /// </summary>
    /// <param name="factory">The geometry factory used to construct the Point.</param>
    /// <returns>A GeoJSON-serializable Point <see cref="Feature"/>.</returns>
    public Feature ToFeature(GeometryFactory factory)
    {
        AttributesTable attrs = new()
        {
            { "id", Id.ToString() },
            { "type", "node" },
            { "version", Version },
            { "editState", (int)EditState.Fetched },
            { "fill", "#fff" },
            { "stroke", "#555" }
        };

        foreach ((string k, string v) in Tags)
        {
            attrs.Add("tag:" + k, v);
        }

        return new Feature(factory.CreatePoint(new Coordinate(Lon, Lat)), attrs);
    }
}
