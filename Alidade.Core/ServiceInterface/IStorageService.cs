namespace Alidade.Core.ServiceInterface;

/// <summary>
///   Interface for a service that handles storage of persistant data in the application.
/// </summary>
public interface IStorageService
{
    /// <summary>
    ///   Resolves the bearer token for the currently selected account on the given endpoint,
    ///   or <see langword="null"/> if no account is selected or the selection no longer
    ///   matches any stored account.
    /// </summary>
    /// <param name="endpoint">The endpoint to resolve the token for.</param>
    /// <returns>The bearer token string, or null.</returns>
    public Task<string?> GetActiveTokenAsync(ApiEndpoints endpoint);

    /// <summary>
    ///   Records which account is currently active for the given endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to set the selection for.</param>
    /// <param name="userId">The OSM user ID of the selected account.</param>
    public Task SetSelectedAccountAsync(ApiEndpoints endpoint, long userId);

    /// <summary>
    ///   Clears the active account selection for the given endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint to clear the selection for.</param>
    public Task ClearSelectedAccountAsync(ApiEndpoints endpoint);

    /// <summary>
    ///   Retrieves cached tile XML by key, or <see langword="null"/> if not cached.
    /// </summary>
    /// <param name="key">The tile cache key.</param>
    /// <returns>The cached tile XML string, or null.</returns>
    public ValueTask<string?> GetTileAsync(string key);

    /// <summary>
    ///   Stores tile XML under the given key.
    /// </summary>
    /// <param name="key">The tile cache key.</param>
    /// <param name="data">The tile XML string to cache.</param>
    public ValueTask SetTileAsync(string key, string data);

    /// <summary>
    ///   Removes all tile cache entries older than the TTL.
    /// </summary>
    public ValueTask EvictOldTilesAsync();
}
