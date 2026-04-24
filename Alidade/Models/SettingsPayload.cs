namespace Alidade.Models;

/// <summary>
///   The unified settings blob stored both in IndexedDB and as an OSM user preference.
/// </summary>
/// <param name="SavedAt">When this payload was last written.</param>
/// <param name="ActiveEndpoint">The endpoint last selected by the user.</param>
/// <param name="KeyBindings">Keybinding overrides keyed by action ID.</param>

public record SettingsPayload(DateTimeOffset SavedAt, string ActiveEndpoint, Dictionary<string, string> KeyBindings);