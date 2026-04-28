namespace Alidade.Core.ServiceInterface;

/// <summary>
///   HTTP-only contract for the OSM API v0.6 notes endpoint.
///   Returns the raw JSON response string; parsing is handled by a dedicated Questy handler.
/// </summary>
public interface IOsmNotesService
{
    /// <summary>
    ///   Fetches the GeoJSON for notes within the specified bounding box as a stream.
    ///   The caller is responsible for disposing the returned stream.
    /// </summary>
    /// <param name="bbox">The geographic bounding box to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream over the GeoJSON response body.</returns>
    /// <exception cref="OperationCanceledException">The request was cancelled via <paramref name="cancellationToken"/>.</exception>
    /// <exception cref="ArgumentNullException">The resolved API base URL is null.</exception>
    /// <exception cref="HttpRequestException">The HTTP request failed or the server returned a non-success status code.</exception>
    /// <exception cref="UriFormatException">The resolved API base URL is not a valid URI.</exception>
    public Task<Stream> FetchNotesAsync(Bbox bbox, CancellationToken cancellationToken);
}
