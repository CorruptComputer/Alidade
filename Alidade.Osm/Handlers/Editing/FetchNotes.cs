using Alidade.Osm.Handlers.Parsing;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class FetchNotes(IOsmNotesService osmNotes, ISender sender) : IRequestHandler<FetchNotes.Query, QueryResult<OsmNote[]>>
{
    /// <summary>
    ///   Fetches OSM notes for the given bounding box.
    /// </summary>
    /// <param name="Bbox">The geographic bounding box to fetch.</param>
    public record Query(Bbox Bbox) : IRequest<QueryResult<OsmNote[]>>;

    /// <inheritdoc />
    public async Task<QueryResult<OsmNote[]>> Handle(Query request, CancellationToken cancellationToken)
    {
        await using Stream stream = await osmNotes.FetchNotesAsync(request.Bbox, cancellationToken);

        QueryResult<IList<OsmNote>> parseResult =
            await sender.Send(new ParseNotesJson.Query(stream), cancellationToken);

        if (!parseResult.Success || parseResult.Result is null)
        {
            return QueryResult<OsmNote[]>.Fail(parseResult.FailReason);
        }

        return parseResult.Result.ToArray();
    }
}
