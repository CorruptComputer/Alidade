using System.Xml.Linq;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class BuildOsmChangeXml : IRequestHandler<BuildOsmChangeXml.Query, QueryResult<string>>
{
    /// <summary>
    ///   Builds the osmChange XML for the given changeset ID and set of edits.
    /// </summary>
    /// <param name="ChangesetId">The open changeset to tag all elements with.</param>
    /// <param name="Change">The creates, modifies, and deletes to encode.</param>
    public record Query(int ChangesetId, OsmChange Change) : IRequest<QueryResult<string>>;

    /// <inheritdoc />
    public Task<QueryResult<string>> Handle(Query request, CancellationToken cancellationToken)
    {
        int csId = request.ChangesetId;
        OsmChange change = request.Change;

        static XElement NodeEl(OsmNode n, int id)
            => new("node",
                new XAttribute("id", n.Id),
                new XAttribute("version", n.Version),
                new XAttribute("changeset", id),
                new XAttribute("lat", n.Lat.ToString("F7")),
                new XAttribute("lon", n.Lon.ToString("F7")),
                n.Tags.Select(kv => new XElement("tag", new XAttribute("k", kv.Key), new XAttribute("v", kv.Value))));

        static XElement WayEl(OsmWay w, int id)
            => new("way",
                new XAttribute("id", w.Id),
                new XAttribute("version", w.Version),
                new XAttribute("changeset", id),
                w.NodeIds.Select(nid => new XElement("nd", new XAttribute("ref", nid))),
                w.Tags.Select(kv => new XElement("tag", new XAttribute("k", kv.Key), new XAttribute("v", kv.Value))));

        static XElement RelEl(OsmRelation r, int id)
            => new("relation",
                new XAttribute("id", r.Id),
                new XAttribute("version", r.Version),
                new XAttribute("changeset", id),
                r.Members.Select(m => new XElement("member",
                    new XAttribute("type", m.Type.ToString().ToLowerInvariant()),
                    new XAttribute("ref", m.Ref),
                    new XAttribute("role", m.Role))),
                r.Tags.Select(kv => new XElement("tag",
                    new XAttribute("k", kv.Key), new XAttribute("v", kv.Value))));

        XElement doc = new("osmChange",
            new XElement("create",
                change.CreatedNodes.Select(n => NodeEl(n, csId)),
                change.CreatedWays.Select(w => WayEl(w, csId)),
                change.CreatedRelations.Select(r => RelEl(r, csId))),
            new XElement("modify",
                change.ModifiedNodes.Select(n => NodeEl(n, csId)),
                change.ModifiedWays.Select(w => WayEl(w, csId)),
                change.ModifiedRelations.Select(r => RelEl(r, csId))),
            new XElement("delete",
                change.DeletedRelationVersions.Select(kv => new XElement("relation",
                    new XAttribute("id", kv.Key), new XAttribute("version", kv.Value),
                    new XAttribute("changeset", csId))),
                change.DeletedWayVersions.Select(kv => new XElement("way",
                    new XAttribute("id", kv.Key), new XAttribute("version", kv.Value),
                    new XAttribute("changeset", csId))),
                change.DeletedNodeVersions.Select(kv => new XElement("node",
                    new XAttribute("id", kv.Key), new XAttribute("version", kv.Value),
                    new XAttribute("changeset", csId)))));

        string xml = new XDocument(doc).ToString();
        return Task.FromResult<QueryResult<string>>(xml);
    }
}
