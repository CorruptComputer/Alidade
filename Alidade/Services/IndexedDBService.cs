using Alidade.Interop;

namespace Alidade.Services;

/// <summary>
///   Domain-aware IndexedDB access layer. Centralises all key naming conventions and
///   typed serialization so that no other service needs to know raw key strings or
///   call <see cref="IndexedDbInteropService"/> directly.
///   <para>
///     Data stored per domain:
///     <list type="bullet">
///       <item><term>Auth accounts</term><description><c>auth_accounts:{endpoint}</c> <see cref="StoredAccount"/> array for that endpoint.</description></item>
///       <item><term>Selected account</term><description><c>selected_account:{endpoint}</c> <see cref="long"/> user ID of the active account.</description></item>
///       <item><term>Settings</term><description><c>alidade_settings</c> <see cref="SettingsPayload"/>.</description></item>
///       <item><term>Edit buffer draft</term><description><c>edit_buffer_draft</c> <see cref="EditBufferDraft"/>.</description></item>
///       <item><term>Tile cache</term><description>Delegated directly to <see cref="IndexedDbInteropService"/>.</description></item>
///     </list>
///   </para>
/// </summary>
public class IndexedDBService(IndexedDbInteropService db) : IStorageService
{
    private static string AccountsKey(ApiEndpoints endpoint) => $"auth_accounts:{endpoint}";
    private static string SelectedAccountKey(ApiEndpoints endpoint) => $"selected_account:{endpoint}";

    private const string SettingsKey = "alidade_settings";
    private const string DraftKey = "edit_buffer_draft";

    #region Auth accounts

    /// <summary>
    ///   Returns all stored accounts for the given endpoint, or an empty list if none exist.
    /// </summary>
    /// <param name="endpoint">The endpoint to look up accounts for.</param>
    /// <returns>The stored account list, never null.</returns>
    public async Task<List<StoredAccount>> GetStoredAccountsAsync(ApiEndpoints endpoint)
    {
        try
        {
            List<StoredAccount>? accounts = await db.GetAsync<List<StoredAccount>>(AccountsKey(endpoint));
            return accounts ?? [];
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///   Persists the account list for the given endpoint. Deletes the key when the list is empty.
    /// </summary>
    /// <param name="endpoint">The endpoint the accounts belong to.</param>
    /// <param name="accounts">The accounts to store.</param>
    public async Task SaveStoredAccountsAsync(ApiEndpoints endpoint, List<StoredAccount> accounts)
    {
        if (accounts.Count == 0)
        {
            await db.DeleteAsync(AccountsKey(endpoint));
        }
        else
        {
            await db.SetAsync(AccountsKey(endpoint), accounts);
        }
    }

    /// <summary>
    ///   Records which account is currently active for the given endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to set the selection for.</param>
    /// <param name="userId">The OSM user ID of the selected account.</param>
    public async Task SetSelectedAccountAsync(ApiEndpoints endpoint, long userId)
        => await db.SetAsync(SelectedAccountKey(endpoint), userId);

    /// <summary>
    ///   Clears the active account selection for the given endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to clear the selection for.</param>
    public async Task ClearSelectedAccountAsync(ApiEndpoints endpoint)
        => await db.DeleteAsync(SelectedAccountKey(endpoint));

    /// <summary>
    ///   Resolves the bearer token for the currently selected account on the given endpoint.
    ///   Returns <see langword="null"/> if no account is selected or the selection no longer
    ///   matches any stored account.
    /// </summary>
    /// <param name="endpoint">The endpoint to resolve the token for.</param>
    /// <returns>The bearer token string, or null.</returns>
    public async Task<string?> GetActiveTokenAsync(ApiEndpoints endpoint)
    {
        long? selectedId = await db.GetAsync<long?>(SelectedAccountKey(endpoint));
        if (selectedId is null)
        {
            return null;
        }

        List<StoredAccount> accounts = await GetStoredAccountsAsync(endpoint);
        return accounts.Find(a => a.UserId == selectedId.Value)?.Token;
    }

    #endregion

    #region Settings

    /// <summary>
    ///   Returns the persisted settings payload, or <see langword="null"/> if none has been saved.
    /// </summary>
    /// <returns>The stored settings, or null.</returns>
    public async Task<SettingsPayload?> GetSettingsAsync()
    {
        try
        {
            return await db.GetAsync<SettingsPayload>(SettingsKey);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    ///   Persists the settings payload.
    /// </summary>
    /// <param name="payload">The settings payload to store.</param>
    public async Task SaveSettingsAsync(SettingsPayload payload)
        => await db.SetAsync(SettingsKey, payload);

    #endregion

    #region Edit buffer draft

    /// <summary>
    ///   Returns the persisted edit buffer draft, or <see langword="null"/> if none exists.
    /// </summary>
    /// <returns>The stored draft, or null.</returns>
    public async Task<EditBufferDraft?> GetDraftAsync()
        => await db.GetAsync<EditBufferDraft>(DraftKey);

    /// <summary>
    ///   Persists the edit buffer draft.
    /// </summary>
    /// <param name="draft">The draft to store.</param>
    public async Task SaveDraftAsync(EditBufferDraft draft)
        => await db.SetAsync(DraftKey, draft);

    /// <summary>
    ///   Removes the persisted edit buffer draft.
    /// </summary>
    public async Task DeleteDraftAsync()
        => await db.DeleteAsync(DraftKey);

    #endregion

    #region Tile cache

    /// <summary>
    ///   Retrieves cached tile XML by key, or <see langword="null"/> if not cached.
    /// </summary>
    /// <param name="key">The tile cache key, use <see cref="IndexedDbInteropService.TileKey"/>.</param>
    /// <returns>The cached tile XML string, or null.</returns>
    public ValueTask<string?> GetTileAsync(string key)
        => db.GetTileAsync(key);

    /// <summary>
    ///   Stores tile XML under the given key.
    /// </summary>
    /// <param name="key">The tile cache key, use <see cref="IndexedDbInteropService.TileKey"/>.</param>
    /// <param name="data">The tile XML string to cache.</param>
    public ValueTask SetTileAsync(string key, string data)
        => db.SetTileAsync(key, data);

    /// <summary>
    ///   Removes all tile cache entries older than the TTL.
    /// </summary>
    public ValueTask EvictOldTilesAsync()
        => db.EvictOldTilesAsync();

    /// <summary>
    ///   Constructs the canonical tile cache key for slippy tile coordinates.
    /// </summary>
    /// <param name="z">The zoom level.</param>
    /// <param name="x">The tile X coordinate.</param>
    /// <param name="y">The tile Y coordinate.</param>
    /// <returns>A string key in the format <c>z{z}/{x}/{y}</c>.</returns>
    public static string TileKey(int z, int x, int y)
        => IndexedDbInteropService.TileKey(z, x, y);

    #endregion
}
