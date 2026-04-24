namespace Alidade.Core.Models.Settings;

/// <summary>
///   Immutable user keybinding configuration. Stores only the actions whose combo differs
///   from the catalog default; all other actions fall through to
///   <see cref="KeyBindingCatalog"/>.
///   <para>
///     Use <see cref="WithOverride"/> and <see cref="WithoutOverride"/> to produce
///     modified copies. <see cref="Overrides"/> is exposed for JSON serialization only.
///   </para>
/// </summary>
public sealed class KeyBindingsConfig
{
    /// <summary>
    ///   User-configured overrides keyed by action ID. Exposed for serialization;
    ///   prefer <see cref="GetCombo"/> and <see cref="FindAction"/> for lookups.
    /// </summary>
    public Dictionary<string, string> Overrides { get; init; } = [];

    /// <summary>
    ///   Initializes an empty config (all bindings use catalog defaults).
    /// </summary>
    public KeyBindingsConfig() { }

    private KeyBindingsConfig(Dictionary<string, string> overrides)
    {
        Overrides = overrides;
    }

    /// <summary>
    ///   Returns the effective combo for <paramref name="actionId"/>: the user override
    ///   if one exists, otherwise the catalog default.
    /// </summary>
    public string GetCombo(string actionId)
    {
        if (Overrides.TryGetValue(actionId, out string? combo))
        {
            return combo;
        }

        return KeyBindingCatalog.FindDef(actionId)?.DefaultCombo ?? string.Empty;
    }

    /// <summary>
    ///   Finds the action ID bound to <paramref name="combo"/>, checking user overrides
    ///   first, then catalog defaults for non-overridden actions.
    ///   Returns null if no action owns the combo.
    /// </summary>
    public string? FindAction(string combo)
    {
        if (string.IsNullOrEmpty(combo))
        {
            return null;
        }

        foreach ((string actionId, string c) in Overrides)
        {
            if (c == combo)
            {
                return actionId;
            }
        }

        foreach (KeyBinding def in KeyBindingCatalog.All)
        {
            if (!Overrides.ContainsKey(def.ActionId) && def.DefaultCombo == combo)
            {
                return def.ActionId;
            }
        }

        return null;
    }

    /// <summary>
    ///   Returns true when <paramref name="combo"/> is already in use by another action.
    /// </summary>
    public bool IsComboTaken(string combo, string forActionId)
    {
        string? owner = FindAction(combo);
        return owner is not null && owner != forActionId;
    }

    /// <summary>
    ///   Returns a new config with <paramref name="actionId"/> bound to <paramref name="combo"/>.
    /// </summary>
    public KeyBindingsConfig WithOverride(string actionId, string combo)
    {
        Dictionary<string, string> next = new(Overrides);
        next[actionId] = combo;
        return new KeyBindingsConfig(next);
    }

    /// <summary>
    ///   Returns a new config with the override for <paramref name="actionId"/> removed.
    /// </summary>
    public KeyBindingsConfig WithoutOverride(string actionId)
    {
        if (!Overrides.ContainsKey(actionId))
        {
            return this;
        }

        Dictionary<string, string> next = new(Overrides);
        next.Remove(actionId);
        return new KeyBindingsConfig(next);
    }

    /// <summary>
    ///   Returns a new empty config (resets all bindings to catalog defaults).
    /// </summary>
    public static KeyBindingsConfig Default() => new();

    /// <summary>
    ///   Reconstructs a config from a raw overrides dictionary.
    /// </summary>
    public static KeyBindingsConfig FromOverrides(Dictionary<string, string> overrides)
        => new(new Dictionary<string, string>(overrides));
}
