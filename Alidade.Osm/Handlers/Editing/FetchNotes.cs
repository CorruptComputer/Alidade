using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Handlers.Parsing;
using Alidade.Osm.Models;
using Questy;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class FetchNotes(IOsmNotesService osmNotes, ISender sender) : IRequestHandler<FetchNotes.Query, QueryResult<OsmNote[]>>
{
    /// <summary>
    ///   Fetches OSM notes for the given bounding box.
    /// </summary>
    /// <param name="West">Western longitude bound.</param>
    /// <param name="South">Southern latitude bound.</param>
    /// <param name="East">Eastern longitude bound.</param>
    /// <param name="North">Northern latitude bound.</param>
    public record Query(double West, double South, double East, double North)
        : IRequest<QueryResult<OsmNote[]>>;

    /// <inheritdoc />
    public async Task<QueryResult<OsmNote[]>> Handle(Query request, CancellationToken cancellationToken)
    {
        string json = await osmNotes.FetchNotesAsync(
            request.West, request.South, request.East, request.North, cancellationToken);

        QueryResult<IList<OsmNote>> parseResult =
            await sender.Send(new ParseNotesJson.Query(json), cancellationToken);

        if (!parseResult.Success || parseResult.Result is null)
        {
            return QueryResult<OsmNote[]>.Fail(parseResult.FailReason);
        }

        return parseResult.Result.ToArray();
    }
}
