using System.Xml;
using Alidade.Osm.Models.Parsing;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class ParseOsmXml : IRequestHandler<ParseOsmXml.Query, QueryResult<ParseOsmXmlResult>>
{
    /// <summary>
    ///   Parses OSM XML from <paramref name="Stream"/> into typed element collections.
    ///   The caller retains ownership of the stream and is responsible for disposing it.
    /// </summary>
    /// <param name="Stream">A readable stream positioned at the start of the OSM XML response.</param>
    public record Query(Stream Stream) : IRequest<QueryResult<ParseOsmXmlResult>>
    {
        /// <inheritdoc />
        public override string ToString() => "ParseOsmXml: streaming";
    }

    /// <inheritdoc />
    public async Task<QueryResult<ParseOsmXmlResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        try
        {
            // BrowserHttpReadStream (WASM) only supports async reads, but XmlReader.Create(Stream)
            // performs a synchronous read during encoding detection. Buffer the response bytes
            // into a MemoryStream first — this also avoids the UTF-16 string allocation that
            // GetStringAsync + StringReader would incur.
            using MemoryStream buffer = new();
            await request.Stream.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;

            List<OsmNode> nodes = [];
            List<OsmWay> ways = [];
            List<OsmRelation> relations = [];

            using XmlReader reader = XmlReader.Create(buffer,
                new XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true });

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                switch (reader.Name)
                {
                    case "node":
                        nodes.Add(ReadNode(reader));
                        break;

                    case "way":
                        ways.Add(ReadWay(reader));
                        break;

                    case "relation":
                        relations.Add(ReadRelation(reader));
                        break;
                }
            }

            return new ParseOsmXmlResult(nodes, ways, relations);
        }
        catch (XmlException ex)
        {
            return QueryResult<ParseOsmXmlResult>.Fail(ex.Message);
        }
    }

    private static OsmNode ReadNode(XmlReader r)
    {
        long id = long.Parse(r.GetAttribute("id")!);
        int version = int.Parse(r.GetAttribute("version") ?? "1");
        int? changeset = r.GetAttribute("changeset") is { } cs ? int.Parse(cs) : null;
        string? user = r.GetAttribute("user");
        DateTimeOffset? timestamp = r.GetAttribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts) : null;
        double lat = double.Parse(r.GetAttribute("lat")!);
        double lon = double.Parse(r.GetAttribute("lon")!);

        Dictionary<string, string> tags = [];
        if (!r.IsEmptyElement)
        {
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }

                if (r.NodeType == XmlNodeType.Element && r.Name == "tag")
                {
                    tags[r.GetAttribute("k")!] = r.GetAttribute("v")!;
                }
            }
        }

        return new OsmNode(id, version, changeset, user, timestamp, lat, lon, tags);
    }

    private static OsmWay ReadWay(XmlReader r)
    {
        long id = long.Parse(r.GetAttribute("id")!);
        int version = int.Parse(r.GetAttribute("version") ?? "1");
        int? changeset = r.GetAttribute("changeset") is { } cs ? int.Parse(cs) : null;
        string? user = r.GetAttribute("user");
        DateTimeOffset? timestamp = r.GetAttribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts) : null;

        List<long> nodeRefs = [];
        Dictionary<string, string> tags = [];
        if (!r.IsEmptyElement)
        {
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }

                if (r.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (r.Name == "nd")
                {
                    nodeRefs.Add(long.Parse(r.GetAttribute("ref")!));
                }
                else if (r.Name == "tag")
                {
                    tags[r.GetAttribute("k")!] = r.GetAttribute("v")!;
                }
            }
        }

        return new OsmWay(id, version, changeset, user, timestamp, [.. nodeRefs], tags);
    }

    private static OsmRelation ReadRelation(XmlReader r)
    {
        long id = long.Parse(r.GetAttribute("id")!);
        int version = int.Parse(r.GetAttribute("version") ?? "1");
        int? changeset = r.GetAttribute("changeset") is { } cs ? int.Parse(cs) : null;
        string? user = r.GetAttribute("user");
        DateTimeOffset? timestamp = r.GetAttribute("timestamp") is { } ts ? DateTimeOffset.Parse(ts) : null;

        List<OsmMember> members = [];
        Dictionary<string, string> tags = [];
        if (!r.IsEmptyElement)
        {
            while (r.Read())
            {
                if (r.NodeType == XmlNodeType.EndElement)
                {
                    break;
                }

                if (r.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                if (r.Name == "member")
                {
                    OsmElementTypes type = r.GetAttribute("type") switch
                    {
                        "node" => OsmElementTypes.Node,
                        "way" => OsmElementTypes.Way,
                        _ => OsmElementTypes.Relation
                    };
                    members.Add(new OsmMember(type, long.Parse(r.GetAttribute("ref")!), r.GetAttribute("role") ?? string.Empty));
                }
                else if (r.Name == "tag")
                {
                    tags[r.GetAttribute("k")!] = r.GetAttribute("v")!;
                }
            }
        }

        return new OsmRelation(id, version, changeset, user, timestamp, [.. members], tags);
    }
}
