namespace Alidade.Osm.Models.Tagging;

/// <summary>
///   A named group of related presets shown in the preset browser.
/// </summary>
/// <param name="Id">The category's unique identifier, used for referencing in presets and the UI.</param>
/// <param name="Icon">
///   The name of the category's icon, encoded as <c>{family}-{name}</c>. Categories use a
///   subset of the families available to presets: <c>maki-</c>, <c>temaki-</c>, and
///   <c>iD-</c> only. See <see cref="Preset.Icon"/> for the full family descriptions.
/// </param>
/// <param name="Members">
///   Preset IDs (in display order) that belong to this category, using path-style identifiers
///   such as <c>"waterway/stream"</c> or <c>"highway/service"</c>.
/// </param>
public record PresetCategory(string Id, string? Icon, IReadOnlyList<string> Members);
