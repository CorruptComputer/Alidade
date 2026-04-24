namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable OSM way for draft persistence.
/// </summary>
public class DraftWay
{
    /// <summary>
    ///  OSM element ID (negative for locally created ways).
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
    ///   Ordered list of member node IDs.
    /// </summary>
    public long[] NodeIds { get; set; } = [];

    /// <summary>
    ///   Tag key/value pairs.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];
}
