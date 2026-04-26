using Alidade.Core.Services;

namespace Alidade.Osm.Services;

/// <summary>
///   Implements <see cref="IOsmApiContext"/> by delegating to the Alidade runtime state services.
///   Bridges the library-level OSM API services to the application's storage service and active-endpoint setting.
/// </summary>
/// <param name="storage">The storage service used to retrieve the stored OAuth bearer token.</param>
/// <param name="settings">The settings state service used to resolve the active API endpoint.</param>
internal sealed class OsmApiContext(IStorageService storage, SettingsStateService settings)
    : IOsmApiContext
{
    /// <inheritdoc />
    public string ApiBase
        => ApiEndpointCatalog.Endpoints[settings.GetActiveEndpoint()].ApiBase;

    /// <inheritdoc />
    public Task<string?> GetActiveTokenAsync()
        => storage.GetActiveTokenAsync(settings.GetActiveEndpoint());
}
