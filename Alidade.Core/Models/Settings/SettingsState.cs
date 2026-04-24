using System.Diagnostics.CodeAnalysis;

namespace Alidade.Core.Models.Settings;

/// <summary>
///   Holds user-configurable application settings that persist across sessions, and when authenticated via OSM user preferences.
///   Tracks the active API endpoint, any pending endpoint switch, and keybindings.
/// </summary>
public sealed record SettingsState
{
    /// <summary>
    ///   Initializes with the dev OpenStreetMap endpoint, no pending switch,
    ///   all keybindings at their catalog defaults, and no remote conflict.
    /// </summary>
    [SetsRequiredMembers]
    public SettingsState() : this(ApiEndpoints.OpenStreetMapDev, null, new KeyBindingsConfig(), null) { }

    /// <summary>
    ///   Initializes a new instance of the <see cref="SettingsState"/> record with the specified values.
    /// </summary>
    /// <param name="activeEndpoint">The currently active API endpoint.</param>
    /// <param name="pendingEndpoint">An optional API endpoint that the user has selected but not yet switched to.</param>
    /// <param name="keyBindings">The user's configured keybindings for various actions.</param>
    /// <param name="remoteConflict">Keybindings fetched from the server that differ from local settings, pending user resolution.</param>
    [SetsRequiredMembers]
    public SettingsState(ApiEndpoints activeEndpoint, ApiEndpoints? pendingEndpoint, KeyBindingsConfig keyBindings, KeyBindingsConfig? remoteConflict)
    {
        ActiveEndpoint = activeEndpoint;
        PendingEndpoint = pendingEndpoint;
        KeyBindings = keyBindings;
        RemoteConflict = remoteConflict;
    }

    /// <summary>
    ///   The currently active API endpoint that the application is using for OSM interactions.
    /// </summary>
    public required ApiEndpoints ActiveEndpoint { get; init; }

    /// <summary>
    ///   If the user has selected a different API endpoint but has not yet confirmed the switch, this holds the pending endpoint.
    /// </summary>
    public required ApiEndpoints? PendingEndpoint { get; init; }

    /// <summary>
    ///   The user's configured keybindings for various actions.
    /// </summary>
    public required KeyBindingsConfig KeyBindings { get; init; }

    /// <summary>
    ///   Keybindings fetched from the server that differ from local settings, pending user resolution.
    /// </summary>
    public required KeyBindingsConfig? RemoteConflict { get; init; }
}
