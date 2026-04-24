using Alidade.Osm.Enums;

namespace Alidade.Osm.Models;

/// <summary>
///   The URLs associated with a single <see cref="ApiEndpoints"/> value.
/// </summary>
/// <param name="DisplayName">Human-readable label shown in the settings dropdown.</param>
/// <param name="ApiBase">
///   The OSM API v0.6 base URL, without a trailing slash
///   (e.g. <c>https://api.openstreetmap.org/api/0.6</c>).
/// </param>
/// <param name="OsmBaseUrl">
///   The OAuth 2.0 authorization server base URL, without a trailing slash
///   (e.g. <c>https://www.openstreetmap.org</c>).
/// </param>
/// <param name="ClientId">
///   The OAuth 2.0 client ID registered with this endpoint's authorization server.
///   Each environment (production, dev, OHM) issues its own credentials.
/// </param>
/// <param name="State">
///   Whether the endpoint is fully enabled, read-only, or disabled.
/// </param>
public record ApiEndpointInfo(string DisplayName, string ApiBase, string OsmBaseUrl, string ClientId, EndpointState State);
