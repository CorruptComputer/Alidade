namespace Alidade.Osm.Models.Api.Auth;

/// <summary>
///   Basic profile information returned by the OSM API after a successful login.
/// </summary>
/// <param name="UserName">The user's OSM display name.</param>
/// <param name="UserId">The numeric OSM user ID.</param>
public record OsmUserInfo(string UserName, long UserId);
