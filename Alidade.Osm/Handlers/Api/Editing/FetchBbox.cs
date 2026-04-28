using System.Net;
using System.Net.Http;
using NetTopologySuite.Geometries;
using Alidade.Osm.Handlers.Parsing;
using Alidade.Osm.Models.Editing;
using Alidade.Osm.Models.Parsing;

namespace Alidade.Osm.Handlers.Api.Editing;

/// <inheritdoc />
public sealed class FetchBbox(IOsmEditingService osm, ISender sender)
    : IRequestHandler<FetchBbox.Query, QueryResult<FetchBboxResult>>
{
    /// <summary>
    ///   Fetches OSM nodes, ways, and relations for the given bounding box,
    ///   splitting into sub-requests if the OSM API rejects the area as too large.
    /// </summary>
    /// <param name="Bbox">The geographic bounding box to fetch.</param>
    public record Query(Bbox Bbox) : IRequest<QueryResult<FetchBboxResult>>;

    private const int MaxSplitDepth = 3;

    /// <inheritdoc />
    public Task<QueryResult<FetchBboxResult>> Handle(Query request, CancellationToken cancellationToken)
        => FetchWithSplitAsync(request.Bbox, depth: 0, cancellationToken);

    private async Task<QueryResult<FetchBboxResult>> FetchWithSplitAsync(
        Bbox bbox, int depth, CancellationToken cancellationToken)
    {
        Stream? stream = await FetchBboxStreamAsync(bbox, cancellationToken);

        if (stream is null)
        {
            if (depth >= MaxSplitDepth)
            {
                return QueryResult<FetchBboxResult>.Fail("OSM bbox area too large to fetch.");
            }

            double west = bbox.NorthWest.X;
            double north = bbox.NorthWest.Y;
            double east = bbox.SouthEast.X;
            double south = bbox.SouthEast.Y;
            bool splitOnLongitude = (east - west) >= (north - south);

            Bbox half1 = splitOnLongitude
                ? new Bbox(bbox.NorthWest, new Coordinate((west + east) / 2, south))
                : new Bbox(bbox.NorthWest, new Coordinate(east, (south + north) / 2));

            Bbox half2 = splitOnLongitude
                ? new Bbox(new Coordinate((west + east) / 2, north), bbox.SouthEast)
                : new Bbox(new Coordinate(west, (south + north) / 2), bbox.SouthEast);

            Task<QueryResult<FetchBboxResult>> t1 = FetchWithSplitAsync(half1, depth + 1, cancellationToken);
            Task<QueryResult<FetchBboxResult>> t2 = FetchWithSplitAsync(half2, depth + 1, cancellationToken);
            await Task.WhenAll(t1, t2);

            QueryResult<FetchBboxResult> r1 = t1.Result;
            QueryResult<FetchBboxResult> r2 = t2.Result;

            if (!r1.Success || r1.Result is null || !r2.Success || r2.Result is null)
            {
                return QueryResult<FetchBboxResult>.Fail(r1.FailReason ?? r2.FailReason);
            }

            return new FetchBboxResult(
                [.. r1.Result.Nodes, .. r2.Result.Nodes],
                [.. r1.Result.Ways, .. r2.Result.Ways],
                [.. r1.Result.Relations, .. r2.Result.Relations]);
        }

        await using Stream s = stream;
        QueryResult<ParseOsmXmlResult> parseResult = await sender.Send(new ParseOsmXml.Query(s), cancellationToken);
        if (!parseResult.Success || parseResult.Result is null)
        {
            return QueryResult<FetchBboxResult>.Fail(parseResult.FailReason);
        }
        return new FetchBboxResult(parseResult.Result.Nodes, parseResult.Result.Ways, parseResult.Result.Relations);
    }

    // Returns null when the OSM API returns HTTP 400 (area or node limit exceeded).
    private async Task<Stream?> FetchBboxStreamAsync(Bbox bbox, CancellationToken cancellationToken)
    {
        try
        {
            return await osm.FetchBboxAsync(bbox, cancellationToken);
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.BadRequest)
        {
            return null;
        }
    }
}
