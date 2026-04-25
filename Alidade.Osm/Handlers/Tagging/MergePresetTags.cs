using Alidade.Osm.Models.Tagging;

namespace Alidade.Osm.Handlers.Tagging;

/// <inheritdoc />
public sealed class MergePresetTags
    : IRequestHandler<MergePresetTags.Query, QueryResult<Dictionary<string, string>>>
{
    /// <summary>
    ///   Merges a preset's tags over an existing tag dictionary and removes
    ///   <c>area=yes</c> when it is redundant because an area-implying key
    ///   (e.g. <c>landuse</c>, <c>building</c>, <c>natural</c>) is already
    ///   present in the merged result.
    /// </summary>
    /// <param name="CurrentTags">The element's current tags, used as the merge base.</param>
    /// <param name="Preset">The preset whose tags are applied on top.</param>
    public record Query( IReadOnlyDictionary<string, string> CurrentTags, Preset Preset)
        : IRequest<QueryResult<Dictionary<string, string>>>;

    /// <inheritdoc />
    public Task<QueryResult<Dictionary<string, string>>> Handle( Query request, CancellationToken cancellationToken)
    {
        Dictionary<string, string> merged = new(request.CurrentTags);
        foreach ((string k, string v) in request.Preset.Tags)
        {
            if (v != "*")
            {
                merged[k] = v;
            }
        }

        if (merged.TryGetValue("area", out string? areaVal)
            && areaVal == "yes"
            && merged.Keys.Any(OsmWay.AreaImplyingKeys.Contains))
        {
            merged.Remove("area");
        }

        return Task.FromResult<QueryResult<Dictionary<string, string>>>(merged);
    }
}
