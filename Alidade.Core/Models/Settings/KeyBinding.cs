namespace Alidade.Core.Models.Settings;

/// <summary>
///   A key binding, representing a single user-bindable editor action and its associated key combo.
/// </summary>
/// <param name="ActionId">Stable string identifier for the action.</param>
/// <param name="DisplayName">Human-readable label shown in the settings UI.</param>
/// <param name="Category">UI grouping: "Tools", "Edit", or "View".</param>
/// <param name="DefaultCombo">The default key combo string (e.g. <c>"ctrl+z"</c>).</param>
public sealed record KeyBinding(string ActionId, string DisplayName, string Category, string DefaultCombo);
