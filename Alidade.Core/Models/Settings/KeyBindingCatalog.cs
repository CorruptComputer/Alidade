namespace Alidade.Core.Models.Settings;

/// <summary>
///   Static catalog of all user-bindable editor actions.
///   Each entry carries a display name, UI category, and default key combo.
///   The catalog is never persisted, only user overrides are stored.
/// </summary>
public static class KeyBindingCatalog
{
    /// <summary>
    ///   All bindable actions in display order.
    /// </summary>
    public static readonly IReadOnlyList<KeyBinding> All =
    [
        // Tools
        new(KeyBindingActions.SelectTool, "Select", "Tools", "s"),
        new(KeyBindingActions.DrawNode, "Add Node", "Tools", "a"),
        new(KeyBindingActions.DrawWay, "Draw Way", "Tools", "w"),
        new(KeyBindingActions.DrawArea, "Draw Area", "Tools", "shift+w"),
        new(KeyBindingActions.CancelTool, "Cancel", "Tools", "escape"),

        // Edit
        new(KeyBindingActions.Undo, "Undo", "Edit", "ctrl+z"),
        new(KeyBindingActions.Redo, "Redo", "Edit", "ctrl+y"),
        new(KeyBindingActions.DeleteSelected, "Delete", "Edit", "delete"),
        new(KeyBindingActions.Orthogonalize, "Square Corners", "Edit", "q"),
        new(KeyBindingActions.Circularize, "Circularize", "Edit", "o"),
        new(KeyBindingActions.SplitWay, "Split Way", "Edit", "x"),
        new(KeyBindingActions.Upload, "Upload Changes", "Edit", "ctrl+s"),
        new(KeyBindingActions.Gridify, "Gridify", "Edit", "g"),

        // View
        new(KeyBindingActions.ToggleBackground, "Background", "View", "b"),
        new(KeyBindingActions.ToggleSettings, "Settings", "View", ","),
        new(KeyBindingActions.ToggleValidation, "Validation", "View", "v"),
        new(KeyBindingActions.ToggleUndoHistory, "Undo History", "View", "u"),
        new(KeyBindingActions.FocusSearch, "Search Presets", "View", "/"),
    ];

    private static readonly Dictionary<string, KeyBinding> _byId
        = All.ToDictionary(d => d.ActionId, StringComparer.Ordinal);

    private static readonly IReadOnlyList<string> _modifierKeys = ["control", "shift", "alt", "meta"];

    /// <summary>
    ///   Returns the catalog entry for <paramref name="actionId"/>, or null
    ///   if no action with that ID exists.
    /// </summary>
    public static KeyBinding? FindDef(string actionId)
        => _byId.TryGetValue(actionId, out KeyBinding? def) ? def : null;

    #region Combo normalization
    /// <summary>
    ///   Normalizes keyboard event state into a canonical combo string.
    ///   Returns <see langword="null"/> for modifier-only keypresses.
    /// </summary>
    /// <param name="ctrlKey">Whether the Ctrl modifier is held.</param>
    /// <param name="altKey">Whether the Alt modifier is held.</param>
    /// <param name="shiftKey">Whether the Shift modifier is held.</param>
    /// <param name="key">The key value string (e.g. <c>"z"</c>, <c>"ArrowUp"</c>).</param>
    /// <returns>A normalized combo string such as <c>"ctrl+z"</c>, or <see langword="null"/> for modifier-only presses.</returns>
    public static string? NormalizeCombo(bool ctrlKey, bool altKey, bool shiftKey, string key)
    {
        string k = key.ToLowerInvariant();

        if (_modifierKeys.Contains(k))
        {
            return null;
        }

        List<string> parts = [];
        if (ctrlKey)  { parts.Add("ctrl"); }
        if (altKey)   { parts.Add("alt"); }
        if (shiftKey) { parts.Add("shift"); }
        parts.Add(k);
        return string.Join("+", parts);
    }

    /// <summary>
    ///   Formats a combo string for display, e.g. <c>"ctrl+z"</c> to <c>"Ctrl+Z"</c>.
    ///   Returns <c>"(none)"</c> for an empty or null combo.
    /// </summary>
    /// <param name="combo">The combo string to format.</param>
    /// <returns>A display-friendly combo string, or <c>"(none)"</c>.</returns>
    public static string FormatCombo(string? combo)
    {
        if (string.IsNullOrEmpty(combo))
        {
            return "(none)";
        }

        return string.Join("+",
            combo.Split('+').Select(p => p.Length == 0 ? p
                : char.ToUpperInvariant(p[0]) + p[1..]));
    }
    #endregion
}
