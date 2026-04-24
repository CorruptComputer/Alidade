namespace Alidade.Core.ServiceInterface;

/// <summary>
///   Provides the runtime context that the OSM API services need to reach the
///   active API endpoint and authenticate requests. Implemented in the application layer
///   so that the library has no direct dependency on Blazor state services.
/// </summary>
public interface IOsmApiContext
{
    /// <summary>
    ///   Gets the base URL of the currently active OSM API endpoint
    ///   (e.g. <c>https://api.openstreetmap.org/api/0.6</c>).
    /// </summary>
    string ApiBase { get; }

    /// <summary>
    ///   Returns the bearer token for the currently selected account on the active
    ///   endpoint, or <see langword="null"/> if no account is authenticated.
    /// </summary>
    /// <returns>The bearer token string, or null.</returns>
    Task<string?> GetActiveTokenAsync();
}
