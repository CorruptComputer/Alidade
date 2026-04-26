namespace Alidade.Osm.Models.Api.Auth;

/// <summary>
///   A saved OSM account entry for a specific endpoint, persisted in IndexedDB so that
///   sessions survive page reloads and multiple accounts per endpoint can be stored.
/// </summary>
/// <param name="Username">The user's OSM display name.</param>
/// <param name="UserId">The numeric OSM user ID.</param>
/// <param name="Token">The OAuth 2.0 bearer token for this account.</param>
public record StoredAccount(string Username, long UserId, string Token);