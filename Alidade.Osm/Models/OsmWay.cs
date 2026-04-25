using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models;

/// <summary>
///   An OSM way (linear or area geometry) with an ordered list of node references and tags.
/// </summary>
/// <param name="Id">
///   The way's numeric ID. Negative values are temporary placeholder IDs for locally created
///   ways that have not yet been uploaded.
/// </param>
/// <param name="Version">
///   The server-assigned version number, incremented on each upload. <c>0</c> for locally
///   created ways that have never been uploaded.
/// </param>
/// <param name="ChangesetId">The ID of the changeset that last modified this way, or null for locally created ways.</param>
/// <param name="UserName">The OSM username of the last editor, or null if not available.</param>
/// <param name="Timestamp">The UTC time of the last server-side edit, or null for locally created ways.</param>
/// <param name="NodeIds">
///   Ordered list of node IDs that form the way's geometry. For closed ways the first and
///   last entries are the same node ID. May contain negative IDs for locally created nodes
///   that are part of the same unsaved edit.
/// </param>
/// <param name="Tags">Key-value pairs describing the way.</param>
public sealed record OsmWay(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTimeOffset? Timestamp,
    IReadOnlyList<long> NodeIds,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>
    ///   Returns a typed reference to this way.
    /// </summary>
    public OsmElementRef Ref => new(OsmElementTypes.Way, Id);

    /// <summary>
    ///   Returns <see langword="true"/> when the first and last node IDs are identical.
    /// </summary>
    public bool IsClosed => NodeIds.Count >= 2 && NodeIds[0] == NodeIds[^1];

    // Tag keys that make a closed way implicitly an area even without area=yes.
    private static readonly HashSet<string> AreaImplyingKeys =
    [
        "building", "building:part", "landuse", "leisure", "natural",
        "amenity", "shop", "place", "man_made", "military", "aeroway",
        "boundary", "historic"
    ];

    /// <summary>
    ///   Returns <see langword="true"/> when the way is a closed ring that represents an
    ///   area polygon. A closed way is an area when:
    ///   <list type="bullet">
    ///     <item><description><c>area=no</c> is not set (explicit opt-out), and</description></item>
    ///     <item><description><c>area=yes</c> is set, OR any area-implying tag key is present
    ///       (e.g. <c>building</c>, <c>landuse</c>, <c>leisure</c>, <c>natural</c>…).</description></item>
    ///   </list>
    /// </summary>
    public bool IsArea
        => IsClosed
            && Tags.GetValueOrDefault("area") != "no"
            && (Tags.GetValueOrDefault("area") == "yes"
                || Tags.Keys.Any(k => AreaImplyingKeys.Contains(k)));

    /// <summary>
    ///   Converts this to an NTS <see cref="Feature"/> with a LineString or
    ///   Polygon geometry. Returns null when fewer than two nodes can be resolved.
    /// </summary>
    /// <param name="nodeIndex">The node dictionary used to resolve node coordinates.</param>
    /// <param name="factory">The geometry factory used to construct the LineString or Polygon.</param>
    /// <returns>
    ///   A GeoJSON-serializable LineString or Polygon <see cref="Feature"/>,
    ///   or null when fewer than two referenced nodes exist in <paramref name="nodeIndex"/>.
    /// </returns>
    public Feature? ToFeature(IReadOnlyDictionary<long, OsmNode> nodeIndex, GeometryFactory factory)
    {
        Coordinate[] coords = [.. NodeIds
            .Where(nodeIndex.ContainsKey)
            .Select(id => new Coordinate(nodeIndex[id].Lon, nodeIndex[id].Lat))];

        if (coords.Length < 2)
        {
            return null;
        }

        // nodeIds in coordinate order (same filtering as coords), used by JS drag to
        // update way geometry when a constituent node is moved.
        string nodeIdsStr = string.Join(",", NodeIds.Where(nodeIndex.ContainsKey));

        string onewayAttr = ResolveOneway(Tags);

        AttributesTable attrs = new()
        {
            { "id", Id.ToString() },
            { "type", "way" },
            { "version", Version },
            { "editState", (int)EditState.Fetched },
            { "area", IsArea ? "yes" : "no" },
            { "nodeIds", nodeIdsStr },
            { "stroke", WayStrokeColor(Tags) },
            { "fill", WayFillColor(Tags) },
            { "oneway", onewayAttr }
        };

        foreach ((string k, string v) in Tags)
        {
            attrs.Add("tag:" + k, v);
        }

        bool ringIsClosed = coords.Length >= 4
            && coords[0].X == coords[^1].X
            && coords[0].Y == coords[^1].Y;

        Geometry geom;
        if (IsArea && ringIsClosed)
        {
            try
            {
                geom = factory.CreatePolygon(coords);
            }
            // Invalid polygon rings (e.g. self-intersecting) will throw an ArgumentException.
            // Fall back to LineString so these can still be edited and re-uploaded with a valid geometry.
            catch (ArgumentException)
            {
                geom = factory.CreateLineString(coords);
            }
        }
        else
        {
            geom = factory.CreateLineString(coords);
        }

        return new Feature(geom, attrs);
    }

    private static string WayStrokeColor(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("highway", out _))
        {
            return "#e892a2";
        }

        if (tags.TryGetValue("waterway", out _))
        {
            return "#77bfe8";
        }

        if (tags.TryGetValue("railway", out _))
        {
            return "#888";
        }

        if (tags.TryGetValue("building", out _))
        {
            return "#c77400";
        }

        if (tags.TryGetValue("landuse", out _))
        {
            return "#9e9e00";
        }

        if (tags.TryGetValue("leisure", out _))
        {
            return "#37a549";
        }

        if (tags.TryGetValue("natural", out _))
        {
            return "#1f7a00";
        }

        if (tags.TryGetValue("amenity", out _))
        {
            return "#734a08";
        }


        return "#555";
    }

    private static string ResolveOneway(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("oneway", out string? oneway))
        {
            if (oneway is "yes" or "1" or "true") return "1";
            if (oneway is "-1" or "reverse")      return "-1";
        }

        // Waterways are inherently directional (water flows from first to last node).
        if (tags.ContainsKey("waterway")) return "1";

        return "0";
    }

    private static string WayFillColor(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.TryGetValue("building", out _))
        {
            return "#f2b05c";
        }

        if (tags.TryGetValue("landuse", out _))
        {
            return "#c8c864";
        }

        if (tags.TryGetValue("leisure", out _))
        {
            return "#83d493";
        }

        if (tags.TryGetValue("natural", out _))
        {
            return "#a8d5a0";
        }

        if (tags.TryGetValue("amenity", out _))
        {
            return "#d4a96a";
        }

        if (tags.TryGetValue("waterway", out _))
        {
            return "#aad3e8";
        }

        return "#aaa";
    }
}
