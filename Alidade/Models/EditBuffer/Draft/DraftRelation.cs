namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable OSM relation for draft persistence.
/// </summary>
public class DraftRelation
{
    /// <summary>
    ///   OSM element ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///   Server-assigned version number.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    ///   Changeset ID the element was last modified in, or null if locally created.
    /// </summary>
    public int? ChangesetId { get; set; }

    /// <summary>
    ///   Ordered list of relation members.
    /// </summary>
    public DraftMember[] Members { get; set; } = [];

    /// <summary>
    ///   Tag key/value pairs.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];
}
