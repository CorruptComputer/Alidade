namespace Alidade.Models;

/// <summary>
///   The settings blob pushed to and fetched from the OSM user preferences API.
///   Does not include the active endpoint, which is device-local only.
/// </summary>
/// <param name="SavedAt">When this payload was last written.</param>
/// <param name="KeyBindings">Keybinding overrides keyed by action ID.</param>
internal record OsmSettingsPayload(DateTimeOffset SavedAt, Dictionary<string, string> KeyBindings);
