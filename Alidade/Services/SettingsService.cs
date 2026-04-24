using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace Alidade.Services;

/// <summary>
///   Persists all user settings to IndexedDB for local storage and, when authenticated,
///   syncs keybindings to and from OSM user preferences so they roam across browsers.
/// </summary>
public class SettingsService(
    IndexedDBService storage,
    HttpClient http,
    SettingsStateService settingsState)
{
    /// <summary>
    ///   When the settings were last written, or null if never.
    /// </summary>
    public DateTimeOffset? LastSaved { get; private set; }

    /// <summary>
    ///   Whether the last push to the OSM API succeeded.
    /// </summary>
    public bool IsBackedUp { get; private set; }

    /// <summary>
    ///   Raised whenever <see cref="LastSaved"/> or <see cref="IsBackedUp"/> changes.
    /// </summary>
    public event Action? StatusChanged;

    private const string OsmPrefKey = "alidade:settings";

    #region IndexedDB

    /// <summary>
    ///   Reads the settings payload from IndexedDB and applies endpoint and keybinding settings.
    /// </summary>
    public async Task LoadFromStorageAsync()
    {
        SettingsPayload? payload = await storage.GetSettingsAsync();
        LastSaved = payload?.SavedAt;

        if (payload?.ActiveEndpoint is not null
            && Enum.TryParse(payload.ActiveEndpoint, out ApiEndpoints endpoint)
            && ApiEndpointCatalog.Endpoints.ContainsKey(endpoint))
        {
            ApplyEndpointSwitch(endpoint);
        }

        if (payload?.KeyBindings is { Count: > 0 })
        {
            settingsState.SetState(settingsState.State with
            {
                KeyBindings = KeyBindingsConfig.FromOverrides(payload.KeyBindings)
            });
        }
    }

    /// <summary>
    ///   Persists the current settings to IndexedDB.
    /// </summary>
    public async Task SaveToStorageAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await storage.SaveSettingsAsync(BuildLocalPayload(now));
        LastSaved = now;
        StatusChanged?.Invoke();
    }

    #endregion

    #region OSM API

    /// <summary>
    ///   Fetches the keybinding settings from OSM user preferences and compares them
    ///   against the current local settings. If they differ, sets <see cref="SettingsState.RemoteConflict"/>
    ///   so the user can choose which settings to keep. The active endpoint is never read
    ///   from the remote payload.
    /// </summary>
    public async Task FetchPrefsFromOsmAsync()
    {
        string? token = await storage.GetActiveTokenAsync(settingsState.State.ActiveEndpoint);
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        string prefsUrl = $"{ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].ApiBase}/user/preferences";
        try
        {
            using HttpRequestMessage req = new(HttpMethod.Get, prefsUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            HttpResponseMessage resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                return;
            }

            string xml = await resp.Content.ReadAsStringAsync();
            XDocument doc = XDocument.Parse(xml);
            string? value = doc.Descendants("preference")
                .FirstOrDefault(e => (string?)e.Attribute("k") == OsmPrefKey)
                ?.Attribute("v")?.Value;

            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            OsmSettingsPayload? remote = null;
            try { remote = JsonSerializer.Deserialize<OsmSettingsPayload>(value); } catch { }

            if (remote?.KeyBindings is null)
            {
                return;
            }

            Dictionary<string, string> localOverrides = settingsState.State.KeyBindings.Overrides;
            bool differs = !DictionariesAreEqual(remote.KeyBindings, localOverrides);

            if (differs)
            {
                settingsState.SetState(settingsState.State with
                {
                    RemoteConflict = KeyBindingsConfig.FromOverrides(remote.KeyBindings)
                });
            }
        }
        catch
        {
            // Best-effort; silently ignore network / parse failures.
        }
    }

    /// <summary>
    ///   Pushes the current keybinding settings to OSM user preferences.
    ///   The active endpoint is not included in the remote payload.
    /// </summary>
    public async Task PushPrefsToOsmAsync()
    {
        if (LastSaved is null)
        {
            return;
        }

        string? token = await storage.GetActiveTokenAsync(settingsState.State.ActiveEndpoint);
        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        string putUrl = $"{ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].ApiBase}/user/preferences/{Uri.EscapeDataString(OsmPrefKey)}";
        try
        {
            string json = JsonSerializer.Serialize(BuildOsmPayload(LastSaved.Value));
            using HttpRequestMessage req = new(HttpMethod.Put, putUrl);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = new StringContent(json, Encoding.UTF8, "text/plain");
            HttpResponseMessage resp = await http.SendAsync(req);
            IsBackedUp = resp.IsSuccessStatusCode;
        }
        catch
        {
            IsBackedUp = false;
        }
        finally
        {
            StatusChanged?.Invoke();
        }
    }

    #endregion

    #region State mutations (called by handlers)

    /// <summary>
    ///   Stores the desired endpoint as pending (shown in the confirmation dialog).
    /// </summary>
    public void RequestEndpointSwitch(ApiEndpoints target)
        => settingsState.SetState(settingsState.State with { PendingEndpoint = target });

    /// <summary>
    ///   Activates the new endpoint and clears the pending switch.
    /// </summary>
    public void ApplyEndpointSwitch(ApiEndpoints target)
        => settingsState.SetState(settingsState.State with { ActiveEndpoint = target, PendingEndpoint = null });

    /// <summary>
    ///   Cancels the pending endpoint switch without changing the active one.
    /// </summary>
    public void CancelEndpointSwitch()
        => settingsState.SetState(settingsState.State with { PendingEndpoint = null });

    /// <summary>
    ///   Updates or clears a single keybinding override.
    /// </summary>
    public void UpdateKeyBinding(string actionId, string combo)
    {
        KeyBindingsConfig updated = string.IsNullOrEmpty(combo)
            ? settingsState.State.KeyBindings.WithoutOverride(actionId)
            : settingsState.State.KeyBindings.WithOverride(actionId, combo);
        settingsState.SetState(settingsState.State with { KeyBindings = updated });
    }

    /// <summary>
    ///   Resets all keybindings to catalog defaults.
    /// </summary>
    public void ResetAllKeyBindings()
        => settingsState.SetState(settingsState.State with { KeyBindings = KeyBindingsConfig.Default() });

    /// <summary>
    ///   Dismisses the remote keybinding conflict, keeping the current local settings,
    ///   and pushes them to OSM to overwrite the remote.
    /// </summary>
    public async Task AcceptLocalSettingsAsync()
    {
        settingsState.SetState(settingsState.State with { RemoteConflict = null });
        await SaveAndPushAsync();
    }

    /// <summary>
    ///   Applies the conflicting remote keybindings as the new local state,
    ///   clears the conflict, and saves to IndexedDB.
    /// </summary>
    public async Task AcceptRemoteSettingsAsync()
    {
        KeyBindingsConfig? conflict = settingsState.State.RemoteConflict;
        if (conflict is null)
        {
            return;
        }

        settingsState.SetState(settingsState.State with { KeyBindings = conflict, RemoteConflict = null });
        await SaveToStorageAsync();

        IsBackedUp = true;
        StatusChanged?.Invoke();
    }

    /// <summary>
    ///   Saves keybindings to IndexedDB and pushes them to OSM user preferences.
    /// </summary>
    public Task SaveAndPushAsync()
        => Task.WhenAll(SaveToStorageAsync(), PushPrefsToOsmAsync());

    #endregion

    private SettingsPayload BuildLocalPayload(DateTimeOffset savedAt)
        => new(savedAt, settingsState.State.ActiveEndpoint.ToString(), settingsState.State.KeyBindings.Overrides);

    private OsmSettingsPayload BuildOsmPayload(DateTimeOffset savedAt)
        => new(savedAt, settingsState.State.KeyBindings.Overrides);

    private static bool DictionariesAreEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> pair in a)
        {
            if (!b.TryGetValue(pair.Key, out string? val) || val != pair.Value)
            {
                return false;
            }
        }

        return true;
    }
}
