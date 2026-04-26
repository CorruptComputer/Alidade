using NetTopologySuite.Features;

namespace Alidade.Osm.Services;

/// <summary>
///   Session-scoped cache for OSM data fetched from the API.
///
///   Tracks the geographic area that has already been fetched so that
///   viewport changes only trigger API calls for the uncached portions.
/// </summary>
public interface IOsmCacheService
{
    /// <summary>
    ///   Returns the sub-bboxes of <paramref name="request"/> whose data is not yet in
    ///   the cache, decomposed into axis-aligned rectangles. Returns an empty list when
    ///   the entire requested area is already cached.
    /// </summary>
    /// <param name="request">The geographic bounding box to test against the cache.</param>
    /// <returns>
    ///   A list of bboxes whose union covers the uncached portion of <paramref name="request"/>,
    ///   or an empty list if the full area is already cached.
    /// </returns>
    public List<CacheBounds> GetGeometryMissBboxes(CacheBounds request);

    /// <summary>
    ///   Unions <paramref name="bounds"/> into the cached area geometry and merges the
    ///   supplied OSM elements into the cache store.
    /// </summary>
    /// <param name="bounds">The geographic bounding box that was just fetched.</param>
    /// <param name="data">The OSM elements returned for that bbox.</param>
    public void AddToCache(CacheBounds bounds, OsmCacheData data);

    /// <summary>
    ///   Returns all cached OSM elements that fall within <paramref name="bounds"/>.
    ///   Ways whose nodes extend outside <paramref name="bounds"/> are included along
    ///   with their out-of-bbox member nodes, matching the OSM API's full-way behaviour.
    /// </summary>
    /// <param name="bounds">The geographic bounding box to retrieve data for.</param>
    /// <returns>The cached OSM elements within <paramref name="bounds"/>.</returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when any part of <paramref name="bounds"/> lies outside the cached area.
    ///   This is intentional during development to surface missing-cache bugs early.
    /// </exception>
    public OsmCacheData GetGeometryFromBbox(CacheBounds bounds);

    /// <summary>
    ///   Returns the cached NTS <see cref="Feature"/> for the given node ID,
    ///   or null when the node was not fetched from the OSM API this session
    ///   (e.g. locally created or edited nodes live only in the edit buffer).
    /// </summary>
    /// <param name="id">The node ID to look up.</param>
    /// <returns>The cached <see cref="Feature"/>, or null if not present.</returns>
    public Feature? GetCachedNodeFeature(long id);

    /// <summary>
    ///   Returns the cached NTS <see cref="Feature"/> for the given way ID,
    ///   or null when the way was not fetched from the OSM API this session.
    /// </summary>
    /// <param name="id">The way ID to look up.</param>
    /// <returns>The cached <see cref="Feature"/>, or null if not present.</returns>
    public Feature? GetCachedWayFeature(long id);

    /// <summary>
    ///   Returns all cached notes whose coordinates fall within <paramref name="bounds"/>.
    ///   Returns whatever is available — does not throw if the area is only partially cached.
    /// </summary>
    /// <param name="bounds">The geographic bounding box to retrieve notes for.</param>
    /// <returns>The cached notes within <paramref name="bounds"/>.</returns>
    public IReadOnlyList<OsmNote> GetNotesFromBbox(CacheBounds bounds);

    /// <summary>
    ///   Resets the cache to its initial empty state, discarding all cached geometry and elements.
    /// </summary>
    public void Clear();
}
