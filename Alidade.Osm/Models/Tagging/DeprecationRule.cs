namespace Alidade.Osm.Models.Tagging;

/// <summary>
///   A tag deprecation rule from the id-tagging-schema, mapping an old tag combination
///   to its recommended replacement.
/// </summary>
/// <param name="Old">
///   The deprecated tag combination to match. A value of <c>"*"</c> is a capture wildcard,
///   it matches any value for that key and makes it available as <c>"$1"</c> in
///   <paramref name="Replace"/> (e.g. <c>artwork=*</c> to <c>artwork_type=$1</c>).
///   Rules may match on multiple keys simultaneously (all must be present to trigger).
/// </param>
/// <param name="Replace">
///   The replacement tag combination, or null if the old tag should simply
///   be removed with no replacement. A value of <c>"*"</c> means carry the matched key's
///   old value through unchanged (used for key renames, e.g. <c>amenity=advertising</c> to
///   <c>advertising=*</c>). A value of <c>"$1"</c> substitutes the wildcard-captured value
///   from <paramref name="Old"/>.
/// </param>
public record DeprecationRule(IReadOnlyDictionary<string, string> Old, IReadOnlyDictionary<string, string>? Replace);
