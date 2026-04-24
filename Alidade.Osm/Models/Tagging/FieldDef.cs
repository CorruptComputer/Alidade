namespace Alidade.Osm.Models.Tagging;

/// <summary>
///   A field definition from the id-tagging-schema, describing how a single tag key
///   (or group of keys) should be presented and validated in the tag editor.
/// </summary>
/// <param name="Id">The field's unique identifier, used for referencing in presets.</param>
/// <param name="Key">
///   The primary tag key this field edits, or an empty string for multi-key fields
///   (e.g. <c>manyCombo</c>, <c>structureRadio</c>) where no single key is primary.
///   For those types, all edited keys are listed in <paramref name="Keys"/> only.
/// </param>
/// <param name="Keys">
///   All tag keys this field edits. For single-key fields this mirrors <paramref name="Key"/>.
///   For multi-key fields (e.g. <c>website</c> which also edits <c>contact:website</c>, or
///   <c>vehicles</c> which edits <c>bus</c>, <c>tram</c>, etc.) this lists every affected key.
///   Used to determine field visibility when matching against a preset's tag set.
/// </param>
/// <param name="Type">
///   Controls how the field is rendered in the tag editor. Actual values from the schema include:
///   <c>combo</c>, <c>typeCombo</c>, <c>semiCombo</c>, <c>manyCombo</c>, <c>multiCombo</c>,
///   <c>directionalCombo</c>, <c>check</c>, <c>defaultCheck</c>, <c>onewayCheck</c>,
///   <c>radio</c>, <c>structureRadio</c>, <c>number</c>, <c>text</c>, <c>textarea</c>,
///   <c>url</c>, <c>identifier</c>, <c>localized</c>, <c>wikidata</c>,
///   <c>date</c>, <c>roadspeed</c>, <c>roadheight</c>.
/// </param>
/// <param name="Options">
///   A fixed list of suggested values for combo-style fields (<c>combo</c>, <c>typeCombo</c>,
///   <c>semiCombo</c>, <c>manyCombo</c>, <c>multiCombo</c>, <c>radio</c>, etc.).
///   Empty for field types that do not present a fixed choice list.
/// </param>
/// <param name="MinValue">The minimum allowed value for <c>number</c> fields; null for all other types.</param>
/// <param name="MaxValue">The maximum allowed value for <c>number</c> fields; null for all other types.</param>
/// <param name="UrlFormat">
///   Only present on <c>identifier</c> fields. A URL template where <c>{value}</c> is replaced
///   with the tag value to produce a link to the external resource
///   (e.g. <c>https://commons.wikimedia.org/wiki/{value}</c>).
/// </param>
/// <param name="Pattern">
///   Only present on <c>identifier</c> fields. A regex the tag value must satisfy
///   (used for client-side validation, e.g. a Wikidata QID pattern).
/// </param>
/// <param name="Universal">
///   When <see langword="true"/> this field is injected into every preset's field list,
///   regardless of whether the preset declares it. Used for broadly applicable tags such as
///   <c>wikipedia</c>, <c>wikidata</c>, <c>website</c>, and <c>wikimedia_commons</c>.
/// </param>
/// <param name="LocationSet">
///   Restricts the field to specific countries or regions. null means the
///   field applies everywhere. When set, the field should only be shown when the edited
///   element is located within the specified region(s).
/// </param>
/// <param name="Label">A human-readable label for the field, used in the UI. If null, the field ID will be used as the label.</param>
public record FieldDef(
    string Id,
    string Key,
    IReadOnlyList<string> Keys,
    string Type,
    IReadOnlyList<string> Options,
    double? MinValue,
    double? MaxValue,
    string? UrlFormat,
    string? Pattern,
    bool Universal,
    LocationSet? LocationSet,
    string? Label);
