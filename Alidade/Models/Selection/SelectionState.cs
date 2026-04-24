namespace Alidade.Models.Selection;

/// <summary>
///   Tracks which OSM elements are currently selected and which is being hovered.
/// </summary>
public record SelectionState(
    ImmutableHashSet<OsmElementRef> Selected,
    OsmElementRef? Hovered)
{
    /// <summary>
    ///   Initializes an empty selection with no hovered element.
    /// </summary>
    public SelectionState() : this([], null) { }

    /// <summary>
    ///   Returns <see langword="true"/> when at least one element is selected.
    /// </summary>
    public bool HasSelection
        => !Selected.IsEmpty;

    /// <summary>
    ///   Returns the single selected element, or null if zero or many are selected.
    /// </summary>
    public OsmElementRef? SingleSelected
        => Selected.Count == 1 ? Selected.First() : null;
}
