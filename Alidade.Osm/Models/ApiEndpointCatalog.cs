using Alidade.Osm.Enums;

namespace Alidade.Osm.Models;

/// <summary>
///   Provides the URL metadata for every <see cref="ApiEndpoints"/> value.
/// </summary>
public static class ApiEndpointCatalog
{
    // TODO: Right now this is only handling dev.alidade-editor.com in release builds but thats all thats being deployed for now.
    //       Once ready for a stable release need to add #if PROD_WEB, #if DEV_WEB, and #if ELECTRON to handle different callback URLs.

    /// <summary>
    ///   All supported endpoints, keyed by their <see cref="ApiEndpoints"/> identifier.
    /// </summary>
    public static readonly IReadOnlyDictionary<ApiEndpoints, ApiEndpointInfo> Endpoints
        = new Dictionary<ApiEndpoints, ApiEndpointInfo>
        {
            [ApiEndpoints.OpenStreetMap] = new(
                "OpenStreetMap",
                "https://api.openstreetmap.org/api/0.6",
                "https://www.openstreetmap.org",
#if DEBUG
                "-ETXQsszNmdK3ebnue7J1wuFoaNAm2rKcKFd6GOmKBU", // http://127.0.0.1:8080/oauth-callback.html
#else
                string.Empty, // https://dev.alidade-editor.com/oauth-callback.html
#endif
                EndpointState.ReadOnly),

            [ApiEndpoints.OpenStreetMapDev] = new(
                "OpenStreetMap (Dev)",
                "https://api06.dev.openstreetmap.org/api/0.6",
                "https://master.apis.dev.openstreetmap.org",
#if DEBUG
                "PTJeDwbplzfvucV_Hn47cah7d-YnXZlewyPD9jI_m7w", // http://127.0.0.1:8080/oauth-callback.html
#else
                "j76m4AfRBidngnC1vk4gaU5JqdGGdUuPa9YaxWiGkAM", // https://dev.alidade-editor.com/oauth-callback.html
#endif
                EndpointState.Enabled),

            [ApiEndpoints.OpenHistoricalMap] = new(
                "OpenHistoricalMap",
                "https://openhistoricalmap.org/api/0.6",
                "https://www.openhistoricalmap.org",
#if DEBUG
                string.Empty, // http://127.0.0.1:8080/oauth-callback.html
#else
                string.Empty, // https://dev.alidade-editor.com/oauth-callback.html
#endif
                EndpointState.Disabled),
        };
}
