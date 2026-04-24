using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alidade.Osm.Models.Api.Auth;

namespace Alidade.Osm.Services;

/// <summary>
///   Pure HTTP client for the OAuth 2.0 PKCE flow against an OSM-compatible authorization
///   server. Handles the token exchange and user-info fetch; all popup management, state
///   dispatch, and account storage are the responsibility of the calling application layer.
/// </summary>
/// <param name="http">The HTTP client used for token exchange and user-info requests.</param>
public class OsmOAuthClient(HttpClient http)
{
    #region Token exchange

    /// <summary>
    ///   Exchanges an authorization code for a bearer token at the given token endpoint.
    /// </summary>
    /// <param name="tokenEndpoint">The full URL of the OAuth2 token endpoint.</param>
    /// <param name="clientId">The registered OAuth2 client ID.</param>
    /// <param name="code">The authorization code received from the redirect.</param>
    /// <param name="verifier">The PKCE code verifier generated at login start.</param>
    /// <param name="redirectUri">The redirect URI used in the initial authorization request.</param>
    /// <returns>The bearer token string.</returns>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the token endpoint response does not contain an <c>access_token</c> field.
    /// </exception>
    public async Task<string> ExchangeCodeAsync(string tokenEndpoint, string clientId, string code, string verifier, string redirectUri)
    {
        FormUrlEncodedContent body = new(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["code"] = code,
            ["code_verifier"] = verifier
        });

        HttpResponseMessage resp = await http.PostAsync(tokenEndpoint, body);
        resp.EnsureSuccessStatusCode();
        string json = await resp.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("No access_token in response.");
    }

    #endregion

    #region User info

    /// <summary>
    ///   Fetches the OSM user profile from the given user-info URL using the supplied
    ///   bearer token. Returns <see langword="null"/> when the server returns a non-success
    ///   status (e.g. token expired).
    /// </summary>
    /// <param name="userInfoUrl">The full URL of the user details endpoint.</param>
    /// <param name="token">The bearer token to authenticate the request with.</param>
    /// <returns>The parsed <see cref="OsmUserInfo"/>, or null on auth failure.</returns>
    public async Task<OsmUserInfo?> FetchUserInfoAsync(string userInfoUrl, string token)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, userInfoUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        HttpResponseMessage resp = await http.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await resp.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement user = doc.RootElement.GetProperty("user");

        return new OsmUserInfo(
            user.GetProperty("display_name").GetString() ?? string.Empty,
            user.TryGetProperty("img", out JsonElement img)
                ? img.GetProperty("href").GetString()
                : null,
            user.GetProperty("id").GetInt64());
    }

    #endregion

    #region PKCE helpers

    /// <summary>
    ///   Generates a cryptographically random PKCE code verifier.
    /// </summary>
    /// <returns>A Base64URL-encoded random string suitable for use as a PKCE verifier.</returns>
    public static string GenerateCodeVerifier()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(96));

    /// <summary>
    ///   Derives the S256 PKCE code challenge from the given verifier.
    /// </summary>
    /// <param name="verifier">The PKCE code verifier to derive the challenge from.</param>
    /// <returns>A Base64URL-encoded SHA-256 hash of the verifier.</returns>
    public static string GenerateCodeChallenge(string verifier)
        => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    /// <summary>
    ///   Generates a cryptographically random OAuth state parameter.
    /// </summary>
    /// <returns>A Base64URL-encoded random string.</returns>
    public static string GenerateState()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    #endregion
}
