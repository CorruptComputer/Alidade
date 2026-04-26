namespace Alidade.Core.ServiceInterface;

/// <summary>
///   HTTP-only contract for the OSM API v0.6 editing endpoints.
///   All methods return raw response strings; parsing is handled by dedicated Questy handlers.
/// </summary>
public interface IOsmEditingService
{
    /// <summary>
    ///   Fetches the OSM XML for all elements within the specified bounding box as a stream.
    ///   The caller is responsible for disposing the returned stream.
    /// </summary>
    /// <param name="west">Western longitude bound.</param>
    /// <param name="south">Southern latitude bound.</param>
    /// <param name="east">Eastern longitude bound.</param>
    /// <param name="north">Northern latitude bound.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>A stream over the OSM XML response body.</returns>
    public Task<Stream> FetchBboxAsync(double west, double south, double east, double north,
        CancellationToken ct = default);

    /// <summary>
    ///   Creates a new OSM changeset with the given tags and returns the assigned changeset ID.
    /// </summary>
    /// <param name="tags">The changeset tags (e.g. <c>comment</c>, <c>created_by</c>).</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The numeric changeset ID assigned by the OSM API.</returns>
    public Task<int> CreateChangesetAsync(Dictionary<string, string> tags,
        CancellationToken ct = default);

    /// <summary>
    ///   Uploads a pre-built osmChange XML document to an open changeset and returns the raw
    ///   diffResult XML string.
    /// </summary>
    /// <param name="changesetId">The open changeset to upload to.</param>
    /// <param name="osmChangeXml">The osmChange XML body to POST.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The raw diffResult XML string returned by the OSM API.</returns>
    public Task<string> UploadChangesetAsync(int changesetId, string osmChangeXml,
        CancellationToken ct = default);

    /// <summary>
    ///   Closes an open changeset, preventing further uploads to it.
    /// </summary>
    /// <param name="changesetId">The changeset ID to close.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public Task CloseChangesetAsync(int changesetId, CancellationToken ct = default);

    /// <summary>
    ///   Fetches the raw OSM XML for a single node from <c>GET /api/0.6/node/{id}</c>.
    /// </summary>
    /// <param name="nodeId">The node ID to fetch.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The raw OSM XML string returned by the API.</returns>
    public Task<string> FetchNodeAsync(long nodeId, CancellationToken ct = default);

    /// <summary>
    ///   Fetches the raw OSM XML for a way and all its constituent nodes from
    ///   <c>GET /api/0.6/way/{id}/full</c>.
    /// </summary>
    /// <param name="wayId">The way ID to fetch.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The raw OSM XML string returned by the API.</returns>
    public Task<string> FetchWayFullAsync(long wayId, CancellationToken ct = default);
}
