using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Alidade.OsmGen.Generators;

/// <summary>
///   Reads the editor-layer-index npm package and emits <c>ImageryData.g.cs</c>
///   containing all usable TMS imagery entries as a static C# collection.
///   Only entries with a usable tile URL template are included; entries requiring
///   an API key or OAuth token are excluded.
/// </summary>
internal static class ImageryGenerator
{
    private static readonly Regex SwitchPattern = new(@"\{switch:([^}]+)\}", RegexOptions.Compiled);
    private static readonly HashSet<string> SkipTemplateTokens = ["apikey", "token", "API_KEY"];

    public static void Generate(string nodeModulesDir, string outputDir)
    {
        Console.WriteLine("ImageryGenerator: reading editor-layer-index...");

        string geojsonPath = Path.Combine(nodeModulesDir, "@openstreetmap", "editor-layer-index", "imagery.geojson");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(geojsonPath));

        JsonElement features = doc.RootElement.GetProperty("features");
        int total = 0, skipped = 0;

        List<EliEntry> entries = [];

        foreach (JsonElement feature in features.EnumerateArray())
        {
            total++;
            if (!feature.TryGetProperty("properties", out JsonElement p))
            {
                skipped++;
                continue;
            }

            // Only TMS sources, no overlays
            if (p.TryGetProperty("type", out JsonElement typeEl) && typeEl.GetString() != "tms")
            {
                skipped++;
                continue;
            }
            if (p.TryGetProperty("overlay", out JsonElement overlayEl) && overlayEl.GetBoolean())
            {
                skipped++;
                continue;
            }

            // Need a URL template
            if (!p.TryGetProperty("url", out JsonElement urlEl))
            {
                skipped++;
                continue;
            }
            string? template = urlEl.GetString();
            if (string.IsNullOrEmpty(template))
            {
                skipped++;
                continue;
            }

            // Skip entries requiring API key / token
            if (SkipTemplateTokens.Any(tok => template.Contains("{" + tok + "}")))
            {
                skipped++;
                continue;
            }

            string? id = p.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
            string? name = p.TryGetProperty("name", out JsonElement nameEl) ? nameEl.GetString() : null;
            if (id is null || name is null)
            {
                skipped++;
                continue;
            }

            bool best = p.TryGetProperty("best", out JsonElement bestEl) && bestEl.GetBoolean();
            string? category = p.TryGetProperty("category", out JsonElement catEl) ? catEl.GetString() : null;
            string? termsText = p.TryGetProperty("attribution", out JsonElement attEl)
                && attEl.TryGetProperty("text", out JsonElement attTextEl) ? attTextEl.GetString() : null;
            string? termsUrl = attEl.ValueKind == JsonValueKind.Object
                && attEl.TryGetProperty("url", out JsonElement attUrlEl) ? attUrlEl.GetString() : null;

            int? tileSize = p.TryGetProperty("tile_size", out JsonElement tsEl) ? tsEl.GetInt32() : null;

            int minZoom = p.TryGetProperty("min_zoom", out JsonElement mnEl) ? mnEl.GetInt32() : 0;
            int maxZoom = p.TryGetProperty("max_zoom", out JsonElement mxEl) ? mxEl.GetInt32() : 22;

            // Polygon coverage, null means worldwide
            double[][][]? polygon = null;
            if (feature.TryGetProperty("geometry", out JsonElement geom) && geom.ValueKind != JsonValueKind.Null)
            {
                polygon = ParsePolygon(geom);
            }

            // Skip entries with no usable MapLibre tile URL after template conversion
            if (!HasUsableUrl(template))
            {
                skipped++;
                continue;
            }

            string? countryCode = p.TryGetProperty("country_code", out JsonElement ccEl) ? ccEl.GetString() : null;

            entries.Add(new EliEntry(id, name, template, minZoom, maxZoom, category, best,
                termsText, termsUrl, tileSize, polygon, CountryCode: countryCode));
        }

        // best=true entries first, then alphabetical by name within each group
        entries = [.. entries.OrderByDescending(e => e.Best).ThenBy(e => e.Name)];

        Console.WriteLine($"  {total} features total → {entries.Count} included, {skipped} skipped");

        // Group by country code. Null country_code = worldwide (applies everywhere).
        // One generated file per group keeps each method small enough for the WASM JIT.
        Dictionary<string, List<EliEntry>> groups = new(StringComparer.Ordinal);
        foreach (EliEntry e in entries)
        {
            string key = e.CountryCode ?? "Worldwide";
            if (!groups.TryGetValue(key, out List<EliEntry>? list))
            {
                list = [];
                groups[key] = list;
            }
            list.Add(e);
        }

        List<string> sortedKeys = [.. groups.Keys.OrderBy(k => k)];

        // Main file: All property + BuildAll() that calls each per-country method.
        {
            StringBuilder sb = new();
            sb.AppendLine(CodeGenHelper.AutoGeneratedHeader);
            sb.AppendLine("using Alidade.Osm.Models.Imagery;");
            sb.AppendLine();
            sb.AppendLine("namespace Alidade.Osm.ImageryLayers;");
            sb.AppendLine();
            sb.AppendLine("public static partial class ImageryData");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>");
            sb.AppendLine("    ///   All usable TMS imagery entries, sorted with <c>best=true</c> entries first.");
            sb.AppendLine("    ///   Worldwide entries come before country-specific ones.");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine($"    public static IReadOnlyList<ImageryEntry> All {{ get; }} = BuildAll();");
            sb.AppendLine();
            sb.AppendLine($"    private static List<ImageryEntry> BuildAll()");
            sb.AppendLine("    {");
            sb.AppendLine($"        List<ImageryEntry> result = new({entries.Count});");
            foreach (string key in sortedKeys)
            {
                string methodName = GroupMethodName(key);
                sb.AppendLine($"        result.AddRange({methodName}());");
            }
            sb.AppendLine("        return result;");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            CodeGenHelper.WriteFile(Path.Combine(outputDir, "ImageryData.g.cs"), sb.ToString());
        }

        // Per-group files
        foreach (string key in sortedKeys)
        {
            List<EliEntry> group = groups[key];
            string methodName = GroupMethodName(key);
            string fileName = $"ImageryData_{key}.g.cs";

            StringBuilder sb = new();
            sb.AppendLine(CodeGenHelper.AutoGeneratedHeader);
            sb.AppendLine("using Alidade.Osm.Models.Imagery;");
            sb.AppendLine();
            sb.AppendLine("namespace Alidade.Osm.ImageryLayers;");
            sb.AppendLine();
            sb.AppendLine("public static partial class ImageryData");
            sb.AppendLine("{");
            sb.AppendLine($"    private static ImageryEntry[] {methodName}() =>");
            sb.AppendLine("    [");

            foreach (EliEntry e in group)
            {
                string zoomExtent = e.MinZoom == 0 && e.MaxZoom == 22
                    ? "null"
                    : $"[{e.MinZoom}, {e.MaxZoom}]";
                string polygon = EmitPolygon(e.Polygon);

                sb.AppendLine($"        new ImageryEntry(");
                sb.AppendLine($"            Id: {CodeGenHelper.StringLiteral(e.Id)},");
                sb.AppendLine($"            Name: {CodeGenHelper.StringLiteral(e.Name)},");
                sb.AppendLine($"            Template: {CodeGenHelper.StringLiteral(e.Template)},");
                sb.AppendLine($"            ZoomExtent: {zoomExtent},");
                sb.AppendLine($"            Category: {CodeGenHelper.StringLiteral(e.Category)},");
                sb.AppendLine($"            Best: {CodeGenHelper.BoolLiteral(e.Best)},");
                sb.AppendLine($"            TermsText: {CodeGenHelper.StringLiteral(e.TermsText)},");
                sb.AppendLine($"            TermsUrl: {CodeGenHelper.StringLiteral(e.TermsUrl)},");
                sb.AppendLine($"            TileSize: {(e.TileSize is null ? "null" : e.TileSize.ToString())},");
                sb.AppendLine($"            Polygon: {polygon}),");
            }

            sb.AppendLine("    ];");
            sb.AppendLine("}");

            CodeGenHelper.WriteFile(Path.Combine(outputDir, fileName), sb.ToString());
            Console.WriteLine($"  wrote {fileName} ({group.Count} entries)");
        }
    }

    #region Helpers
    private static bool HasUsableUrl(string template)
    {
        // Convert like ToMaplibreUrls() does, if result is non-empty, it's usable
        string tpl = template.Replace("{-y}", "{y}").Replace("{zoom}", "{z}");
        Match m = SwitchPattern.Match(tpl);
        if (m.Success)
        {
            return m.Groups[1].Value.Split(',').Length > 0;
        }
        return tpl.Length > 0;
    }

    private static double[][][]? ParsePolygon(JsonElement geom)
    {
        if (!geom.TryGetProperty("type", out JsonElement typeEl))
        {
            return null;
        }
        string? type = typeEl.GetString();
        if (!geom.TryGetProperty("coordinates", out JsonElement coords))
        {
            return null;
        }

        try
        {
            if (type == "Polygon")
            {
                // coords = [ ring, ring, ... ]
                return [ParseRings(coords)];
            }
            if (type == "MultiPolygon")
            {
                // coords = [ [ ring, ring ], ... ]
                return [.. coords.EnumerateArray().Select(poly => ParseRings(poly))];
            }
        }
        catch
        {
            // Malformed geometry, treat as worldwide
        }
        return null;
    }

    private static double[][] ParseRings(JsonElement rings)
    {
        // Take only the outer ring (first element)
        JsonElement outerRing = rings.EnumerateArray().First();
        return [.. outerRing.EnumerateArray()
            .Select(pt =>
            {
                double[] coords = [.. pt.EnumerateArray().Select(v => v.GetDouble())];
                return coords;
            })];
    }

    private static string EmitPolygon(double[][][]? polygon)
    {
        if (polygon is null)
        {
            return "null";
        }

        StringBuilder sb = new("[");
        foreach (double[][] ring in polygon)
        {
            sb.Append("[");
            foreach (double[] pt in ring)
            {
                string lon = pt[0].ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                string lat = pt[1].ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                sb.Append($"[{lon}, {lat}], ");
            }
            if (sb[^2] == ',')
            {
                sb.Length -= 2; // remove trailing ", "
            }
            sb.Append("], ");
        }
        if (sb.Length > 1 && sb[^2] == ',')
        {
            sb.Length -= 2;
        }
        sb.Append(']');
        return sb.ToString();
    }
    #endregion

    private static string GroupMethodName(string key)
        => "Group_" + Regex.Replace(key, @"[^A-Za-z0-9]", "_");

    private sealed record EliEntry(
        string Id,
        string Name,
        string Template,
        int MinZoom,
        int MaxZoom,
        string? Category,
        bool Best,
        string? TermsText,
        string? TermsUrl,
        int? TileSize,
        double[][][]? Polygon,
        string? CountryCode);
}
