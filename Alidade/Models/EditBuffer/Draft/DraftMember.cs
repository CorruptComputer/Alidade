namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable OSM relation member.
/// </summary>
public class DraftMember
{
    /// <summary>
    ///   <see cref="Alidade.Osm.Models.OsmElementTypes"/> cast to int.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    ///   Referenced element ID.
    /// </summary>
    public long Ref { get; set; }

    /// <summary>
    ///   Member role string (may be empty).
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
