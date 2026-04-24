using Microsoft.JSInterop;

namespace Alidade.Interop;

/// <summary>
///   Wraps the <c>window.idbInterop</c> JS object to provide typed access to two IndexedDB
///   object stores: <c>keyvalue</c> for arbitrary JSON blobs and <c>tiles</c> for cached
///   OSM tile XML with a timestamp-based TTL.
/// </summary>
public class IndexedDbInteropService(IJSRuntime js)
{
    private static readonly TimeSpan TileTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    ///   Retrieves a value by key from the <c>keyvalue</c> store, or null
    ///   if the key does not exist.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <returns>The stored value, or null if not found.</returns>
    public ValueTask<T?> GetAsync<T>(string key)
        => js.InvokeAsync<T?>("idbInterop.get", key);

    /// <summary>
    ///   Stores a value under the given key in the <c>keyvalue</c> store.
    /// </summary>
    /// <param name="key">The key to store under.</param>
    /// <param name="value">The value to store.</param>
    /// <typeparam name="T">The value type.</typeparam>
    public ValueTask SetAsync<T>(string key, T value)
        => js.InvokeVoidAsync("idbInterop.set", key, value);

    /// <summary>
    ///   Removes a key from the <c>keyvalue</c> store.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    public ValueTask DeleteAsync(string key)
        => js.InvokeVoidAsync("idbInterop.delete", key);

    /// <summary>
    ///   Retrieves cached tile XML by key, or null if the key does not exist.
    /// </summary>
    /// <param name="key">The tile cache key (see <see cref="TileKey"/>).</param>
    /// <returns>The cached tile XML string, or null if not cached.</returns>
    public ValueTask<string?> GetTileAsync(string key)
        => js.InvokeAsync<string?>("idbInterop.getTile", key);

    /// <summary>
    ///   Stores tile XML under the given key, recording the current timestamp for TTL eviction.
    /// </summary>
    /// <param name="key">The tile cache key (see <see cref="TileKey"/>).</param>
    /// <param name="data">The tile XML string to cache.</param>
    public ValueTask SetTileAsync(string key, string data)
        => js.InvokeVoidAsync("idbInterop.setTile", key, data);

    /// <summary>
    ///   Removes all tile cache entries older than <see cref="TileTtl"/> (5 minutes).
    /// </summary>
    public ValueTask EvictOldTilesAsync()
        => js.InvokeVoidAsync("idbInterop.evictOldTiles", (long)TileTtl.TotalMilliseconds);

    /// <summary>
    ///   Constructs the canonical tile cache key for slippy tile coordinates.
    /// </summary>
    /// <param name="z">The zoom level.</param>
    /// <param name="x">The tile X coordinate.</param>
    /// <param name="y">The tile Y coordinate.</param>
    /// <returns>A string key in the format <c>z{z}/{x}/{y}</c>.</returns>
    public static string TileKey(int z, int x, int y)
        => $"z{z}/{x}/{y}";
}
