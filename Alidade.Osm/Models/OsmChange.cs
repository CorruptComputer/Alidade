namespace Alidade.Osm.Models;

/// <summary>
///   An osmChange document grouping creates, modifies, and deletes to be uploaded in a single
///   changeset. Corresponds to the <c>&lt;osmChange&gt;</c> XML element in the OSM API v0.6 format.
///   Creates and modifies must be ordered nodes → ways → relations so that dependencies are
///   satisfied. Deletes must use the reverse order (relations → ways → nodes) so that members
///   are freed before their containers are removed.
/// </summary>
/// <param name="CreatedNodes">Nodes to be created; must have negative placeholder IDs.</param>
/// <param name="CreatedWays">Ways to be created; must have negative placeholder IDs.</param>
/// <param name="CreatedRelations">Relations to be created; must have negative placeholder IDs.</param>
/// <param name="ModifiedNodes">Nodes that were fetched from the server and have been edited locally.</param>
/// <param name="ModifiedWays">Ways that were fetched from the server and have been edited locally.</param>
/// <param name="ModifiedRelations">Relations that were fetched from the server and have been edited locally.</param>
/// <param name="DeletedNodeVersions">IDs and server versions of nodes to delete.</param>
/// <param name="DeletedWayVersions">IDs and server versions of ways to delete.</param>
/// <param name="DeletedRelationVersions">IDs and server versions of relations to delete.</param>
public record OsmChange(
    IReadOnlyList<OsmNode> CreatedNodes,
    IReadOnlyList<OsmWay> CreatedWays,
    IReadOnlyList<OsmRelation> CreatedRelations,
    IReadOnlyList<OsmNode> ModifiedNodes,
    IReadOnlyList<OsmWay> ModifiedWays,
    IReadOnlyList<OsmRelation> ModifiedRelations,
    ImmutableDictionary<long, int> DeletedNodeVersions,
    ImmutableDictionary<long, int> DeletedWayVersions,
    ImmutableDictionary<long, int> DeletedRelationVersions);
