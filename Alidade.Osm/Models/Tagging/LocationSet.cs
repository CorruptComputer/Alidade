namespace Alidade.Osm.Models.Tagging;

/// <summary>
///   Specifies which countries or regions a preset or field applies to.
///   Codes are ISO 3166-1 alpha-2 country codes (e.g. <c>"us"</c>, <c>"gb"</c>) or
///   Wikidata QIDs for broader geographic regions (e.g. <c>"Q46"</c> for Europe).
/// </summary>
/// <param name="Include">
///   Codes for locations where the preset or field should be shown. An empty list means
///   no inclusion restriction, the item applies everywhere unless explicitly excluded.
/// </param>
/// <param name="Exclude">
///   Codes for locations where the preset or field should be hidden. An empty list means
///   no exclusion restriction. Typically used to suppress a field in a specific country
///   when it is otherwise globally applicable (e.g. hide <c>ref/vatin</c> in the US).
/// </param>
public record LocationSet(IReadOnlyList<string> Include, IReadOnlyList<string> Exclude);
