using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Alidade.Core.Consts;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Models.Imagery;

/// <summary>
///   A single TMS imagery source from the
///   <see href="https://github.com/osmlab/editor-layer-index">Editor Layer Index</see>.
/// </summary>
public partial record ImageryEntry(
    string Id,
    string Name,
    string Template,
    [property: JsonPropertyName("zoomExtent")] int[]? ZoomExtent,
    string? Category,
    bool Best,
    [property: JsonPropertyName("terms_text")] string? TermsText,
    [property: JsonPropertyName("terms_url")] string? TermsUrl,
    [property: JsonPropertyName("tileSize")] int? TileSize,
    // polygon[poly][point] = [lon, lat]; null = worldwide source
    [property: JsonPropertyName("polygon")] double[][][]? Polygon)
{
    /// <summary>Minimum usable zoom level (inclusive).</summary>
    public int MinZoom
        => ZoomExtent?[0] ?? 0;

    /// <summary>
    ///   Maximum usable zoom level (inclusive), or null when the source does not
    ///   specify one. Callers should apply a sensible cap (e.g. 19) when null.
    /// </summary>
    public int? MaxZoom
        => ZoomExtent?[1];

    [GeneratedRegex(@"\{switch:([^}]+)\}")]
    private static partial Regex SwitchTokenRegex();

    /// <summary>
    ///   Converts the iD/ELI tile URL template to one or more MapLibre-compatible
    ///   tile URL strings and indicates whether the TMS y-flip scheme is required.
    /// </summary>
    /// <returns>
    ///   A tuple of the expanded tile URL array and a flag indicating TMS (flipped-y) scheme.
    /// </returns>
    public (string[] Tiles, bool IsTms) ToMaplibreUrls()
    {
        if (string.IsNullOrEmpty(Template))
        {
            return ([], false);
        }

        bool isTms = Template.Contains("{-y}");

        // Normalise y token for TMS sources
        string tpl = Template.Replace("{-y}", "{y}");

        // MapLibre uses {z}, ELI uses {zoom}
        tpl = tpl.Replace("{zoom}", "{z}");

        // Expand {switch:a,b,c} to multiple tile URLs for load balancing
        Match m = SwitchTokenRegex().Match(tpl);

        if (m.Success)
        {
            string[] variants = m.Groups[1].Value.Split(',');
            string[] tiles = [.. variants.Select(v => tpl.Replace(m.Value, v))];
            return (tiles, isTms);
        }

        return ([tpl], isTms);
    }

    /// <summary>
    ///   Builds an NTS <see cref="Geometry"/> for the coverage polygons,
    ///   or returns null for worldwide sources.
    /// </summary>
    public Geometry? BuildCoverageGeometry(GeometryFactory factory)
    {
        if (Polygon is not { Length: > 0 })
        {
            return null;
        }

        Polygon[] polys = [.. Polygon.Select(ring =>
        {
            Coordinate[] coords = [.. ring.Select(p => new Coordinate(p[0], p[1]))];

            // Ensure the ring is closed
            if (!coords[0].Equals(coords[^1]))
            {
                coords = [..coords, coords[0]];
            }

            return factory.CreatePolygon(coords);
        })];

        return polys.Length == 1
            ? polys[0]
            : factory.CreateMultiPolygon(polys);
    }
}
