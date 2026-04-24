namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable OSM node for draft persistence.
/// </summary>
public class DraftNode
{
    /// <summary>
    ///   OSM element ID (negative for locally created nodes).
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
    ///   Latitude in decimal degrees.
    /// </summary>
    public double Lat { get; set; }

    /// <summary>
    ///   Longitude in decimal degrees.
    /// </summary>
    public double Lon { get; set; }

    /// <summary>
    ///   Tag key/value pairs.
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = [];
}
