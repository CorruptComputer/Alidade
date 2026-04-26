using System.Text;
using Alidade.Osm.Handlers.Parsing;
using Alidade.Osm.Models.Editing;
using Alidade.Osm.Models.Parsing;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class FetchElement(IOsmEditingService osm, ISender sender)
    : IRequestHandler<FetchElement.Query, QueryResult<FetchElementResult>>
{
    /// <summary>
    ///   Fetches a single OSM element and any elements required to fully represent it
    ///   (e.g. constituent nodes for a way).
    /// </summary>
    /// <param name="ElementRef">Reference identifying the element to fetch.</param>
    public record Query(OsmElementRef ElementRef) : IRequest<QueryResult<FetchElementResult>>;

    /// <inheritdoc />
    public async Task<QueryResult<FetchElementResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        OsmElementTypes type = request.ElementRef.Type;
        if (type is not OsmElementTypes.Node and not OsmElementTypes.Way)
        {
            return QueryResult<FetchElementResult>.Fail($"Fetching {type} elements is not supported.");
        }

        string xml;
        try
        {
            xml = type == OsmElementTypes.Node
                ? await osm.FetchNodeAsync(request.ElementRef.Id, cancellationToken)
                : await osm.FetchWayFullAsync(request.ElementRef.Id, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return QueryResult<FetchElementResult>.Fail(ex.Message);
        }

        QueryResult<ParseOsmXmlResult> parseResult = await sender.Send(
            new ParseOsmXml.Query(new MemoryStream(Encoding.UTF8.GetBytes(xml))), cancellationToken);
        if (!parseResult.Success || parseResult.Result is null)
        {
            return QueryResult<FetchElementResult>.Fail(parseResult.FailReason);
        }

        return new FetchElementResult(parseResult.Result.Nodes, parseResult.Result.Ways, parseResult.Result.Relations);
    }
}
