using Alidade.Osm.Models.Tagging;
using Alidade.Osm.TaggingSchemas;

namespace Alidade.Osm.Services;

/// <summary>
///   Provides preset search, best-match selection, field resolution, and deprecation
///   lookups for the tag editor and validation system.
///   Data is sourced from the generated <see cref="TaggingData"/>.
/// </summary>
public class PresetService
{
    // Search index: (normalized search text, preset) pairs built once at first use.
    private List<(string Text, Preset Preset)>? _searchIndex;

    /// <summary>
    ///   All loaded presets keyed by their id-tagging-schema ID.
    /// </summary>
    public IReadOnlyDictionary<string, Preset> Presets
        => TaggingData.Presets;

    /// <summary>
    ///   All loaded field definitions keyed by their id-tagging-schema ID.
    /// </summary>
    public IReadOnlyDictionary<string, FieldDef> Fields
        => TaggingData.Fields;

    /// <summary>
    ///   All loaded tag deprecation rules.
    /// </summary>
    public IReadOnlyList<DeprecationRule> DeprecationRules
        => TaggingData.DeprecationRules;

    /// <summary>
    ///   All loaded preset categories keyed by their id-tagging-schema ID.
    /// </summary>
    public IReadOnlyDictionary<string, PresetCategory> Categories
        => TaggingData.Categories;

    #region Tag label

    /// <summary>
    ///   Formats a preset's identifying tags as a human-readable label.
    ///   Non-wildcard tags are joined as <c>key=value</c> pairs separated by <c> | </c>.
    ///   Falls back to the preset ID if all tag values are wildcards or the tag set is empty.
    /// </summary>
    /// <param name="preset">The preset to format.</param>
    /// <returns>
    ///   A label string such as <c>amenity=school</c> or
    ///   <c>highway=service | service=parking_aisle</c>.
    /// </returns>
    public static string FormatTagLabel(Preset preset)
    {
        string label = string.Join(" | ", preset.Tags
            .Where(kv => kv.Value != "*")
            .Select(kv => $"{kv.Key}={kv.Value}"));
        return string.IsNullOrEmpty(label) ? preset.Id : label;
    }

    #endregion

    #region Search
    /// <summary>
    ///   Full-text searches the preset index using space-separated tokens.
    ///   Results are scored and capped at 20 entries.
    /// </summary>
    public IReadOnlyList<Preset> Search(string query, string? geometry = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string[] tokens = query.ToLowerInvariant().Split(' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<(string Text, Preset Preset)> index = GetSearchIndex();

        return [.. index
            .Where(e => e.Preset.Searchable
                && (geometry is null || e.Preset.Geometry.Contains(geometry))
                && tokens.All(t => e.Text.Contains(t)))
            .OrderByDescending(e => ScoreSearch(e.Preset, tokens))
            .Select(e => e.Preset)
            .Take(20)];
    }
    #endregion

    #region Best match
    /// <summary>
    ///   Finds the preset whose required tags best match the given tag dictionary
    ///   for the specified geometry type.
    /// </summary>
    public Preset? BestMatch(IReadOnlyDictionary<string, string> tags, string geometry)
    {
        Preset? best = null;
        double bestScore = -1;

        foreach (Preset preset in TaggingData.Presets.Values)
        {
            if (!preset.Geometry.Contains(geometry))
            {
                continue;
            }

            double score = MatchScore(preset, tags);
            if (score <= 0)
            {
                continue;
            }

            if (score > bestScore || (score == bestScore
                    && (preset.MatchScore > (best?.MatchScore ?? 0))))
            {
                bestScore = score;
                best = preset;
            }
        }

        return best;
    }
    #endregion

    #region Field resolution
    /// <summary>
    ///   Resolves field IDs from a preset into <see cref="FieldDef"/> objects.
    ///   IDs wrapped in braces (e.g. <c>{amenity/cafe}</c> or <c>{@templates/contact}</c>)
    ///   are preset references, the referenced preset's fields are expanded in-place.
    /// </summary>
    public IReadOnlyList<FieldDef> ResolveFields(Preset preset) => ResolveFields(preset, 0);

    private List<FieldDef> ResolveFields(Preset preset, int depth)
    {
        if (depth > 4)
        {
            return [];
        }

        List<FieldDef> result = [];
        foreach (string id in preset.Fields)
        {
            if (id.StartsWith('{') && id.EndsWith('}'))
            {
                // Strip braces and optional leading @ (template marker)
                string refId = id[1..^1].TrimStart('@');
                if (TaggingData.Presets.TryGetValue(refId, out Preset? refPreset))
                {
                    result.AddRange(ResolveFields(refPreset, depth + 1));
                }
            }
            else if (TaggingData.Fields.TryGetValue(id, out FieldDef? f))
            {
                result.Add(f);
            }
        }
        return result;
    }
    #endregion

    #region Deprecation lookup
    /// <summary>
    ///   Returns all deprecation rules whose <c>old</c> tag set is fully satisfied by
    ///   the given element tags. Every key in a rule's <c>old</c> dictionary must be
    ///   present on the element, with either an exact value match or <c>*</c> as wildcard.
    /// </summary>
    /// <remarks>
    ///   Rules with multiple <c>old</c> keys (e.g. <c>direction=down + highway=*</c>)
    ///   require ALL keys to be simultaneously present; checking individual tag pairs
    ///   would produce false positives for those multi-key rules.
    /// </remarks>
    public IEnumerable<DeprecationRule> FindDeprecations(IReadOnlyDictionary<string, string> tags)
        => TaggingData.DeprecationRules.Where(r =>
            r.Old.All(kvp => tags.TryGetValue(kvp.Key, out string? v)
                          && (kvp.Value == v || kvp.Value == "*")));
    #endregion

    /// <summary>
    ///   Pre-builds the search index. Call once at startup to avoid a freeze on
    ///   the user's first preset search or element selection.
    /// </summary>
    public void WarmUp() => GetSearchIndex();

    #region Search
    private List<(string Text, Preset Preset)> GetSearchIndex()
    {
        if (_searchIndex is not null)
        {
            return _searchIndex;
        }

        _searchIndex = [.. TaggingData.Presets.Values
            .Select(p =>
            {
                string text = string.Join(" ", new[]
                {
                    FormatTagLabel(p),
                    string.Join(" ", p.Terms),
                    string.Join(" ", p.Aliases),
                    p.Id.Replace("/", " ").Replace("_", " ")
                }).ToLowerInvariant();
                return (text, p);
            })];

        return _searchIndex;
    }

    private static double ScoreSearch(Preset p, string[] tokens)
    {
        string name = FormatTagLabel(p).ToLowerInvariant();
        double score = tokens.Sum(t => name.StartsWith(t) ? 3 : name.Contains(t) ? 1 : 0);
        score += p.MatchScore * 0.1;
        return score;
    }

    private static double MatchScore(Preset preset, IReadOnlyDictionary<string, string> tags)
    {
        if (preset.Tags.Count == 0)
        {
            return 0;
        }

        double score = 0;
        foreach ((string k, string v) in preset.Tags)
        {
            if (!tags.TryGetValue(k, out string? actual))
            {
                return 0;
            }
            if (v != "*" && v != actual)
            {
                return 0;
            }
            score += v == "*" ? 0.5 : 1.0;
        }
        return score;
    }
    #endregion
}
