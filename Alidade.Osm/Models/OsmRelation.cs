namespace Alidade.Osm.Models;

/// <summary>
///   An OSM relation with an ordered member list and tags.
/// </summary>
/// <param name="Id">
///   The relation's numeric ID. Negative values are temporary placeholder IDs for locally
///   created relations that have not yet been uploaded.
/// </param>
/// <param name="Version">
///   The server-assigned version number, incremented on each upload. <c>0</c> for locally
///   created relations that have never been uploaded.
/// </param>
/// <param name="ChangesetId">The ID of the changeset that last modified this relation, or null for locally created relations.</param>
/// <param name="UserName">The OSM username of the last editor, or null if not available.</param>
/// <param name="Timestamp">The UTC time of the last server-side edit, or null for locally created relations.</param>
/// <param name="Members">Ordered list of elements that belong to this relation, each carrying a type, reference ID, and role.</param>
/// <param name="Tags">Key-value pairs describing the relation (e.g. <c>type=multipolygon</c>, <c>type=route</c>).</param>
public record OsmRelation(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTimeOffset? Timestamp,
    IReadOnlyList<OsmMember> Members,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>
    ///   Returns a typed reference to this relation.
    /// </summary>
    public OsmElementRef Ref => new(OsmElementTypes.Relation, Id);
}
