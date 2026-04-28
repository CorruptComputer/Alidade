namespace Alidade.Osm.Services;

/// <summary>
///   HTTP-only OSM API v0.6 client for the Notes endpoint.
///   Returns the raw GeoJSON response string; parsing is handled by a dedicated Questy handler.
///   The active API base URL is resolved through <see cref="IOsmApiContext"/> on each call.
/// </summary>
/// <param name="http">The HTTP client used for all OSM API requests.</param>
/// <param name="context">Resolves the active API base URL.</param>
internal sealed class OsmNotesService(HttpClient http, IOsmApiContext context) : IOsmNotesService
{
    /// <inheritdoc />
    public Task<Stream> FetchNotesAsync(Bbox bbox, CancellationToken cancellationToken)
    {
        string url = $"{context.ApiBase}/notes.json?bbox={bbox.NorthWest.X:F7},{bbox.SouthEast.Y:F7},{bbox.SouthEast.X:F7},{bbox.NorthWest.Y:F7}&limit=200";
        return http.GetStreamAsync(url, cancellationToken);
    }
}
