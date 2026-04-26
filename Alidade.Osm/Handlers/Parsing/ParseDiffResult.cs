using System.Xml.Linq;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class ParseDiffResult : IRequestHandler<ParseDiffResult.Query, QueryResult<DiffResult>>
{
    /// <summary>
    ///   Parses the given diffResult XML string.
    /// </summary>
    /// <param name="Xml">The raw diffResult XML string returned by the upload endpoint.</param>
    public record Query(string Xml) : IRequest<QueryResult<DiffResult>>;

    /// <inheritdoc />
    public Task<QueryResult<DiffResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        XDocument doc = XDocument.Parse(request.Xml);
        if (doc.Root is null)
        {
            return Task.FromResult(QueryResult<DiffResult>.Fail("Invalid XML: missing root element."));
        }

        XElement root = doc.Root;

        static Dictionary<long, long> MapIds(IEnumerable<XElement> els)
            => els.Where(e => e.Attribute("new_id") is not null)
                  .ToDictionary(e => long.Parse(e.Attribute("old_id")!.Value),
                                e => long.Parse(e.Attribute("new_id")!.Value));

        DiffResult result = new(MapIds(root.Elements("node")), MapIds(root.Elements("way")), MapIds(root.Elements("relation")));

        return Task.FromResult<QueryResult<DiffResult>>(result);
    }
}
