using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace Alidade.Osm.Services;

/// <summary>
///   HTTP-only OSM API v0.6 client for data reading and changeset editing.
///   All methods return raw response strings or primitives; parsing and XML building are
///   handled by dedicated Questy handlers in <c>Alidade.Osm.Handlers.Parsing</c>.
///   The active API base URL and bearer token are resolved through
///   <see cref="IOsmApiContext"/> on each call.
/// </summary>
/// <param name="http">The HTTP client used for all OSM API requests.</param>
/// <param name="context">Resolves the active API base URL and bearer token.</param>
internal sealed class OsmEditingService(HttpClient http, IOsmApiContext context) : IOsmEditingService
{
    private string ApiBase => context.ApiBase;

    #region Bounding box

    /// <inheritdoc />
    public Task<Stream> FetchBboxAsync(Bbox bbox, CancellationToken cancellationToken)
    {
        string url = $"{ApiBase}/map?bbox={bbox.NorthWest.X:F7},{bbox.SouthEast.Y:F7},{bbox.SouthEast.X:F7},{bbox.NorthWest.Y:F7}";
        return http.GetStreamAsync(url, cancellationToken);
    }

    #endregion

    #region Single element fetch

    /// <inheritdoc />
    public Task<string> FetchNodeAsync(long nodeId, CancellationToken cancellationToken)
        => http.GetStringAsync($"{ApiBase}/node/{nodeId}", cancellationToken);

    /// <inheritdoc />
    public Task<string> FetchWayFullAsync(long wayId, CancellationToken cancellationToken)
        => http.GetStringAsync($"{ApiBase}/way/{wayId}/full", cancellationToken);

    #endregion

    #region Changesets

    /// <inheritdoc />
    public async Task<int> CreateChangesetAsync(Dictionary<string, string> tags,
        CancellationToken cancellationToken)
    {
        XElement body = new("osm",
            new XElement("changeset",
                tags.Select(kv => new XElement("tag",
                    new XAttribute("k", kv.Key),
                    new XAttribute("v", kv.Value)))));

        HttpRequestMessage req = new(HttpMethod.Put, $"{ApiBase}/changeset/create")
        {
            Content = new StringContent(body.ToString(), Encoding.UTF8, "application/xml")
        };
        await InjectAuthHeaderAsync(req);
        HttpResponseMessage resp = await http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        return int.Parse(await resp.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<string> UploadChangesetAsync(int changesetId, string osmChangeXml,
        CancellationToken cancellationToken)
    {
        HttpRequestMessage req = new(HttpMethod.Post,
            $"{ApiBase}/changeset/{changesetId}/upload")
        {
            Content = new StringContent(osmChangeXml, Encoding.UTF8, "application/xml")
        };
        await InjectAuthHeaderAsync(req);
        HttpResponseMessage resp = await http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CloseChangesetAsync(int changesetId, CancellationToken cancellationToken)
    {
        HttpRequestMessage req = new(HttpMethod.Put,
            $"{ApiBase}/changeset/{changesetId}/close");
        await InjectAuthHeaderAsync(req);
        HttpResponseMessage resp = await http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    #endregion

    #region Auth helper

    private async Task InjectAuthHeaderAsync(HttpRequestMessage req)
    {
        string? token = await context.GetActiveTokenAsync();
        if (token is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    #endregion
}
