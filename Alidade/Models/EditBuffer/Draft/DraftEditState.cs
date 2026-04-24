namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable edit state entry for a single element.
/// </summary>
public class DraftEditState
{
    /// <summary>
    ///   <see cref="Alidade.Osm.Models.OsmElementTypes"/> cast to int.
    /// </summary>
    public int Type { get; set; }

    /// <summary>
    ///   OSM element ID.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    ///   <see cref="Alidade.Osm.Models.EditState"/> cast to int.
    /// </summary>
    public int State { get; set; }
}
