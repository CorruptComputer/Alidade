namespace Alidade.Core.ServiceInterface;

/// <summary>
///   HTTP-only contract for the OSM API v0.6 notes endpoint.
///   Returns the raw JSON response string; parsing is handled by a dedicated Questy handler.
/// </summary>
public interface IOsmNotesService
{
    /// <summary>
    ///   Fetches the raw GeoJSON string for notes within the specified bounding box.
    /// </summary>
    /// <param name="west">Western longitude bound.</param>
    /// <param name="south">Southern latitude bound.</param>
    /// <param name="east">Eastern longitude bound.</param>
    /// <param name="north">Northern latitude bound.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>The raw GeoJSON FeatureCollection string from the notes endpoint.</returns>
    public Task<string> FetchNotesAsync(double west, double south, double east, double north,
        CancellationToken ct = default);
}
