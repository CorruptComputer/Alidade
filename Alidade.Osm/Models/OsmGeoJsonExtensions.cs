using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models;

/// <summary>
///   Extension methods that convert OSM domain objects into NTS <see cref="Feature"/> instances
///   suitable for serialization as GeoJSON and pushing to MapLibre GL sources.
/// </summary>
public static class OsmGeoJsonExtensions
{
    private static readonly GeometryFactory _geomFactory = new(new PrecisionModel(), 4326);

    /// <summary>
    ///   Converts an <see cref="OsmNode"/> to an NTS <see cref="Feature"/> with a Point geometry.
    ///   Tag key/value pairs are stored as attributes prefixed with <c>tag:</c>.
    /// </summary>
    /// <param name="node">The node to convert.</param>
    /// <returns>A GeoJSON-serializable Point <see cref="Feature"/>.</returns>
    public static Feature ToFeature(this OsmNode node)
    {
        AttributesTable attrs = new()
        {
            { "id", node.Id.ToString() },
            { "type", "node" },
            { "version", node.Version },
            { "editState", (int)EditState.Fetched },
            { "fill", "#fff" },
            { "stroke", "#555" }
        };

        foreach ((string k, string v) in node.Tags)
        {
            attrs.Add("tag:" + k, v);
        }

        return new Feature(_geomFactory.CreatePoint(new Coordinate(node.Lon, node.Lat)), attrs);
    }

    /// <summary>
    ///   Converts an <see cref="OsmWay"/> to an NTS <see cref="Feature"/> with a LineString or
    ///   Polygon geometry. Returns null when fewer than two nodes can be resolved.
    /// </summary>
    /// <param name="way">The way to convert.</param>
    /// <param name="nodeIndex">The node dictionary used to resolve node coordinates.</param>
    /// <returns>
    ///   A GeoJSON-serializable LineString or Polygon <see cref="Feature"/>,
    ///   or null when fewer than two referenced nodes exist in <paramref name="nodeIndex"/>.
    /// </returns>
    public static Feature? ToFeature(this OsmWay way,
        IReadOnlyDictionary<long, OsmNode> nodeIndex)
    {
        Coordinate[] coords = [.. way.NodeIds
            .Where(nodeIndex.ContainsKey)
            .Select(id => new Coordinate(nodeIndex[id].Lon, nodeIndex[id].Lat))];

        if (coords.Length < 2)
        {
            return null;
        }

        // nodeIds in coordinate order (same filtering as coords), used by JS drag to
        // update way geometry when a constituent node is moved.
        string nodeIdsStr = string.Join(",", way.NodeIds.Where(nodeIndex.ContainsKey));

        string onewayAttr = ResolveOneway(way.Tags);

        AttributesTable attrs = new()
        {
            { "id", way.Id.ToString() },
            { "type", "way" },
            { "version", way.Version },
            { "editState", (int)EditState.Fetched },
            { "area", way.IsArea ? "yes" : "no" },
            { "nodeIds", nodeIdsStr },
            { "stroke", WayStrokeColor(way.Tags) },
            { "fill", WayFillColor(way.Tags) },
            { "oneway", onewayAttr }
        };

        foreach ((string k, string v) in way.Tags)
        {
            attrs.Add("tag:" + k, v);
        }

        bool ringIsClosed = coords.Length >= 4
            && coords[0].X == coords[^1].X
            && coords[0].Y == coords[^1].Y;

        Geometry geom;
        if (way.IsArea && ringIsClosed)
        {
            try
            {
                geom = _geomFactory.CreatePolygon(coords);
            }
            // Invalid polygon rings (e.g. self-intersecting) will throw an ArgumentException.
            // Fall back to LineString so these can still be edited and re-uploaded with a valid geometry.
            catch (ArgumentException)
            {
                geom = _geomFactory.CreateLineString(coords);
            }
        }
        else
        {
            geom = _geomFactory.CreateLineString(coords);
        }

        return new Feature(geom, attrs);
    }

    /// <summary>
    ///   Returns a copy of the feature with its <c>editState</c> attribute updated to the given value.
    /// </summary>
    /// <param name="feature">The feature to update.</param>
    /// <param name="state">The new edit state value.</param>
    /// <returns>The same <see cref="Feature"/> instance with the <c>editState</c> attribute updated.</returns>
    public static Feature WithEditState(this Feature feature, EditState state)
    {
        feature.Attributes["editState"] = (int)state;
        return feature;
    }

    /// <summary>
    ///   Returns a MapLibre-compatible stroke colour for a node based on its primary OSM tag.
    /// </summary>
    /// <param name="tags">The tag set of the node.</param>
    /// <returns>A CSS hex colour string.</returns>
    public static string NodeStrokeColor(IReadOnlyDictionary<string, string> tags)
    {
        if (tags.ContainsKey("amenity") || tags.ContainsKey("shop"))
        {
            return "#734a08";
        }

        if (tags.ContainsKey("natural"))
        {
            return "#1f7a00";
        }

        return "#555";
    }

    /// <summary>
    ///   Returns a MapLibre-compatible stroke colour for a way based on its primary OSM tag.
    /// </summary>
    /// <param name="tags">The tag set of the way.</param>
    /// <returns>A CSS hex colour string.</returns>
    public static string WayStrokeColor(IReadOnlyDictionary<string, string> tags)
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

    /// <summary>
    ///   Returns the directional flow value for a way:
    ///   <c>"1"</c> for forward oneway or any waterway,
    ///   <c>"-1"</c> for reverse oneway, or <c>"0"</c> when not directional.
    /// </summary>
    /// <param name="tags">The tag set of the way.</param>
    /// <returns>One of <c>"1"</c>, <c>"-1"</c>, or <c>"0"</c>.</returns>
    public static string ResolveOneway(IReadOnlyDictionary<string, string> tags)
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

    /// <summary>
    ///   Returns a MapLibre-compatible fill colour for an area way based on its primary OSM tag.
    /// </summary>
    /// <param name="tags">The tag set of the way.</param>
    /// <returns>A CSS hex colour string.</returns>
    public static string WayFillColor(IReadOnlyDictionary<string, string> tags)
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
