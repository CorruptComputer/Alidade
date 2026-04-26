using Alidade.Osm.Handlers.Parsing;
using Alidade.Osm.Models.Editing;
using Alidade.Osm.Models.Parsing;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class FetchBbox(IOsmEditingService osm, ISender sender) : IRequestHandler<FetchBbox.Query, QueryResult<FetchBboxResult>>
{
    /// <summary>
    ///   Fetches OSM nodes, ways, and relations for the given bounding box.
    /// </summary>
    /// <param name="West">Western longitude bound.</param>
    /// <param name="South">Southern latitude bound.</param>
    /// <param name="East">Eastern longitude bound.</param>
    /// <param name="North">Northern latitude bound.</param>
    public record Query(double West, double South, double East, double North)
        : IRequest<QueryResult<FetchBboxResult>>;

    /// <inheritdoc />
    public async Task<QueryResult<FetchBboxResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        await using Stream stream = await osm.FetchBboxAsync(request.West, request.South, request.East, request.North, cancellationToken);

        QueryResult<ParseOsmXmlResult> parseResult = await sender.Send(new ParseOsmXml.Query(stream), cancellationToken);
        if (!parseResult.Success || parseResult.Result is null)
        {
            return QueryResult<FetchBboxResult>.Fail(parseResult.FailReason);
        }

        return new FetchBboxResult(parseResult.Result.Nodes, parseResult.Result.Ways, parseResult.Result.Relations);
    }
}
