
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
    public async Task<string> FetchNotesAsync(double west, double south, double east, double north,
        CancellationToken ct = default)
    {
        string url = $"{context.ApiBase}/notes.json?bbox={west:F7},{south:F7},{east:F7},{north:F7}&limit=200";
        return await http.GetStringAsync(url, ct);
    }
}
