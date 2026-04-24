using Alidade.Osm.Models.Nsi;
using Alidade.Osm.NameSuggestions;

namespace Alidade.Osm.Services;

/// <summary>
///   Service for suggesting NSI brand/operator items.
///   Global (worldwide) items are always available. Region-specific items are loaded
///   on demand via <see cref="LoadRegions"/> without touching any other region's data.
/// </summary>
public class NsiService
{
    private static readonly IReadOnlyList<NsiItem> _globalItems = NsiGlobalData.Items;

    // Each loaded region is stored separately so only the accessed classes initialize.
    private List<IReadOnlyList<NsiItem>> _regionLists = [];
    private HashSet<string> _loadedRegionCodes = [];

    /// <summary>
    ///   Determines which GeoJSON regions contain the given map center point and loads
    ///   their NSI items. Should only be called at zoom levels where map data is active.
    ///   Uses precomputed bounding boxes, no polygon geometry is parsed at runtime.
    /// </summary>
    public void UpdateForLocation(double lat, double lon)
    {
        LoadRegions(NsiRegionBounds.GetRegionsForPoint(lat, lon));
    }

    /// <summary>
    ///   Loads NSI items for each of <paramref name="regionCodes"/>.
    ///   Codes can be ISO country codes (<c>"us"</c>) or GeoJSON region refs
    ///   (<c>"us-tx.geojson"</c>). Only the accessed region classes initialize;
    ///   all others remain unloaded.
    ///
    ///   For example, when editing in Texas pass both <c>"us"</c> and
    ///   <c>"us-tx.geojson"</c> to get US-wide and Texas-specific suggestions.
    /// </summary>
    public void LoadRegions(IEnumerable<string> regionCodes)
    {
        HashSet<string> normalized = new(regionCodes.Select(c => c.ToLowerInvariant()), StringComparer.Ordinal);
        if (normalized.SetEquals(_loadedRegionCodes)) return;

        _loadedRegionCodes = normalized;
        _regionLists = [];
        foreach (string code in normalized)
        {
            IReadOnlyList<NsiItem>? items = NsiRegionDispatcher.GetForRegion(code);
            if (items is not null)
                _regionLists.Add(items);
        }
    }

    /// <summary>
    ///   Returns up to ten NSI items whose terms contain <paramref name="nameOrBrand"/>,
    ///   searching global items first then all loaded regions.
    /// </summary>
    public IReadOnlyList<NsiItem> Suggest(string nameOrBrand, string primaryTagKey, string primaryTagValue)
    {
        if (string.IsNullOrWhiteSpace(nameOrBrand)) return [];

        // TODO: filter by primaryTagKey/Value to narrow to the right category
        List<NsiItem> results = [];

        SearchList(_globalItems, nameOrBrand, results, 10);
        foreach (IReadOnlyList<NsiItem> region in _regionLists)
        {
            if (results.Count >= 10) break;
            SearchList(region, nameOrBrand, results, 10 - results.Count);
        }

        return results;
    }

    private static void SearchList(IReadOnlyList<NsiItem> list, string query, List<NsiItem> results, int limit)
    {
        foreach (NsiItem item in list)
        {
            if (results.Count >= limit) break;
            foreach (string term in item.Terms)
            {
                if (term.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(item);
                    break;
                }
            }
        }
    }

    /// <summary>
    ///   Returns how many of the NSI item's match tags agree with the element's tags.
    ///   Use this to pick the most-specific matching entry when multiple candidates exist
    ///   (e.g. an amenity=bank entry scores higher than an amenity=atm entry for a bank element).
    /// </summary>
    public static int CountTagMatches(NsiItem item, IReadOnlyDictionary<string, string> elementTags)
    {
        int count = 0;
        foreach ((string key, string value) in item.Tags)
        {
            if (elementTags.TryGetValue(key, out string? v) && v == value)
                count++;
        }
        return count;
    }

    /// <summary>
    ///   Returns true if any of the NSI item's addTags are absent or differ in the element.
    /// </summary>
    public static bool AnyAddTagMissing(NsiItem item, IReadOnlyDictionary<string, string> elementTags)
    {
        foreach ((string key, string value) in item.AddTags)
        {
            if (!elementTags.TryGetValue(key, out string? v) || v != value)
                return true;
        }
        return false;
    }
}
