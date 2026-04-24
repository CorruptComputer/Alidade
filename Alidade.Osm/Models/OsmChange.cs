namespace Alidade.Osm.Models;

/// <summary>
///   An osmChange document grouping creates, modifies, and deletes to be uploaded in a single
///   changeset. Corresponds to the <c>&lt;osmChange&gt;</c> XML element in the OSM API v0.6 format.
///   Elements must be ordered so that dependencies are satisfied: nodes before ways, ways before
///   relations.
/// </summary>
/// <param name="CreatedNodes">Nodes to be created; must have negative placeholder IDs.</param>
/// <param name="CreatedWays">Ways to be created; must have negative placeholder IDs.</param>
/// <param name="CreatedRelations">Relations to be created; must have negative placeholder IDs.</param>
/// <param name="ModifiedNodes">Nodes that were fetched from the server and have been edited locally.</param>
/// <param name="ModifiedWays">Ways that were fetched from the server and have been edited locally.</param>
/// <param name="ModifiedRelations">Relations that were fetched from the server and have been edited locally.</param>
/// <param name="DeletedNodeIds">IDs of nodes to delete.</param>
/// <param name="DeletedWayIds">IDs of ways to delete.</param>
/// <param name="DeletedRelationIds">IDs of relations to delete.</param>
public record OsmChange(
    IReadOnlyList<OsmNode> CreatedNodes,
    IReadOnlyList<OsmWay> CreatedWays,
    IReadOnlyList<OsmRelation> CreatedRelations,
    IReadOnlyList<OsmNode> ModifiedNodes,
    IReadOnlyList<OsmWay> ModifiedWays,
    IReadOnlyList<OsmRelation> ModifiedRelations,
    IReadOnlyList<long> DeletedNodeIds,
    IReadOnlyList<long> DeletedWayIds,
    IReadOnlyList<long> DeletedRelationIds);
