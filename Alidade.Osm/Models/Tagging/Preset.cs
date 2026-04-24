namespace Alidade.Osm.Models.Tagging;

/// <summary>
///   A preset from the id-tagging-schema, describing a well-known real-world feature type
///   along with its expected tags, applicable geometry types, and search metadata.
/// </summary>
/// <param name="Id">The preset's unique identifier, used for referencing in other presets and categories.</param>
/// <param name="Icon">
///   The name of the preset's icon, encoded as <c>{family}-{name}</c>. Icon families used
///   by the id-tagging-schema are:
///   <list type="bullet">
///     <item><term><c>maki-</c></term><description>Mapbox Maki, the most common family; map-oriented SVG icons.</description></item>
///     <item><term><c>temaki-</c></term><description>Temaki, iD editor's own extended set for OSM-specific features.</description></item>
///     <item><term><c>fas-</c> / <c>far-</c></term><description>Font Awesome Solid / Regular (a small subset).</description></item>
///     <item><term><c>iD-</c></term><description>One-off icons bundled directly in the iD editor.</description></item>
///     <item><term><c>roentgen-</c></term><description>Roentgen, another custom set used by iD.</description></item>
///   </list>
///   May be null if the preset has no icon.
/// </param>
/// <param name="Fields">
///   Ordered list of field IDs to show in the tag editor for this preset.
///   May contain template references in the form <c>{@templates/foo}</c> that must be
///   expanded to concrete field IDs during <c>PresetService</c> load.
/// </param>
/// <param name="MoreFields">
///   Additional field IDs shown in a collapsed "more fields" section. Same expansion rules as
///   <paramref name="Fields"/> apply.
/// </param>
/// <param name="Geometry">
///   OSM geometry types this preset applies to. Possible values are
///   <c>"point"</c>, <c>"vertex"</c>, <c>"line"</c>, <c>"area"</c>, and <c>"relation"</c>.
///   <c>"point"</c> is a standalone node; <c>"vertex"</c> is a node that is part of a way.
/// </param>
/// <param name="Tags">
///   Tag key-value pairs that identify this preset. A value of <c>"*"</c> is a wildcard
///   meaning any value for that key matches (used for broad catch-all presets such as
///   <c>shop=*</c>). Preset matching scores an element by how many of these pairs it satisfies.
/// </param>
/// <param name="MatchScore">
///   A schema-declared weight that biases preset matching. Defaults to <c>1.0</c> when not
///   explicitly set in the schema (most presets). Values below <c>1.0</c> make a preset less
///   likely to win ties; values above (up to <c>2.0</c>) make it more likely.
/// </param>
/// <param name="Name">A human-readable name for the preset, used in the UI. If null, the preset ID will be used as the name.</param>
/// <param name="Terms">
///   Extra search keywords for this preset. Always empty when loaded from the prebuilt dist
///   JSON (terms are stripped during the schema build); only populated if loading from source.
/// </param>
/// <param name="Aliases">
///   Alternative human-readable names (e.g. "Mountaineering Route" for a climbing preset).
///   Like <paramref name="Terms"/>, always empty when loaded from the dist JSON.
/// </param>
/// <param name="Searchable">
///   <see langword="false"/> for abstract category presets (e.g. <c>_aerialway</c>,
///   <c>_amenity</c>) that exist only to group child presets and should never appear in
///   search results. Defaults to <see langword="true"/> for all concrete presets.
/// </param>
public record Preset(
    string Id,
    string? Icon,
    IReadOnlyList<string> Fields,
    IReadOnlyList<string> MoreFields,
    IReadOnlyList<string> Geometry,
    IReadOnlyDictionary<string, string> Tags,
    double MatchScore,
    string? Name,
    IReadOnlyList<string> Terms,
    IReadOnlyList<string> Aliases,
    bool Searchable);
