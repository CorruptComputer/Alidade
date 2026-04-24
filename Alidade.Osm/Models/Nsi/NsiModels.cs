namespace Alidade.Osm.Models.Nsi;

/// <summary>
///   A brand, operator, or chain entry from the Name Suggestion Index (iD preset format).
///   <see cref="Tags"/> are the minimum tags that must match for this entry to apply.
///   <see cref="AddTags"/> is the full tag set that should be applied when the entry is selected.
///   <see cref="Terms"/> includes the display name and alternative spellings for search matching.
/// </summary>
public record NsiItem(
    string DisplayName,
    string Id,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyDictionary<string, string> AddTags,
    IReadOnlyList<string> Terms);
