using System.Xml.Linq;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Models;
using Alidade.Osm.Models.Parsing;
using Questy;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class ParseOsmXml : IRequestHandler<ParseOsmXml.Query, QueryResult<ParseOsmXmlResult>>
{
    /// <summary>
    ///   Parses the given OSM XML string into typed element collections.
    /// </summary>
    /// <param name="Xml">The raw OSM XML string returned by the map endpoint.</param>
    public record Query(string Xml) : IRequest<QueryResult<ParseOsmXmlResult>>;

    /// <inheritdoc />
    public Task<QueryResult<ParseOsmXmlResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        XDocument doc = XDocument.Parse(request.Xml);
        XElement root = doc.Root!;

        List<OsmNode> nodes = [.. root.Elements("node").Select(ParseNode)];
        List<OsmWay> ways = [.. root.Elements("way").Select(ParseWay)];
        List<OsmRelation> relations = [.. root.Elements("relation").Select(ParseRelation)];

        return Task.FromResult<QueryResult<ParseOsmXmlResult>>(new ParseOsmXmlResult(nodes, ways, relations));
    }

    private static OsmNode ParseNode(XElement el)
        => new(long.Parse(el.Attribute("id")!.Value),
               int.Parse(el.Attribute("version")?.Value ?? "1"),
               el.Attribute("changeset") is { } cs ? int.Parse(cs.Value) : null,
               el.Attribute("user")?.Value,
               el.Attribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts.Value) : null,
               double.Parse(el.Attribute("lat")!.Value),
               double.Parse(el.Attribute("lon")!.Value),
               ParseTags(el));

    private static OsmWay ParseWay(XElement el)
        => new(long.Parse(el.Attribute("id")!.Value),
               int.Parse(el.Attribute("version")?.Value ?? "1"),
               el.Attribute("changeset") is { } cs ? int.Parse(cs.Value) : null,
               el.Attribute("user")?.Value,
               el.Attribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts.Value) : null,
               [.. el.Elements("nd").Select(nd => long.Parse(nd.Attribute("ref")!.Value))],
               ParseTags(el));

    private static OsmRelation ParseRelation(XElement el)
        => new(long.Parse(el.Attribute("id")!.Value),
               int.Parse(el.Attribute("version")?.Value ?? "1"),
               el.Attribute("changeset") is { } cs ? int.Parse(cs.Value) : null,
               el.Attribute("user")?.Value,
               el.Attribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts.Value) : null,
               [.. el.Elements("member").Select(
                        m => new OsmMember(m.Attribute("type")!.Value switch
                                           {
                                               "node" => OsmElementTypes.Node,
                                               "way" => OsmElementTypes.Way,
                                               _ => OsmElementTypes.Relation
                                           },
                                           long.Parse(m.Attribute("ref")!.Value),
                                           m.Attribute("role")?.Value ?? string.Empty))
                ],
               ParseTags(el));

    private static Dictionary<string, string> ParseTags(XElement el)
        => el.Elements("tag").ToDictionary(t => t.Attribute("k")!.Value,
                                           t => t.Attribute("v")!.Value);
}
