namespace Alidade.Osm.Models.EditBuffer;

/// <summary>
///   Represents a single conflicted element.
/// </summary>
public class ConflictItem
{
    /// <summary>
    ///   The element reference (type and ID) of the conflicted element.
    /// </summary>
    public required OsmElementRef ElementRef { get; init; }

    /// <summary>
    ///   The tags in the local edit buffer (i.e. the "ours" version).
    /// </summary>
    public required IReadOnlyDictionary<string, string> LocalTags { get; init; }

    /// <summary>
    ///   The tags from the server (i.e. the "theirs" version).
    /// </summary>
    public required IReadOnlyDictionary<string, string> ServerTags { get; init; }

    /// <summary>
    ///   The tags as resolved by the user. Initially empty; populated as the user makes resolutions.
    /// </summary>
    public Dictionary<string, string> Resolved { get; set; } = [];
}
