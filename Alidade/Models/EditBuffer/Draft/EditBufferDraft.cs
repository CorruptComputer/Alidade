namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Serializable snapshot of the dirty portion of the edit buffer, written to IndexedDB
///   after each edit so that unsaved changes survive page refreshes.
///   Only elements with a non-<see cref="Alidade.Osm.Models.EditState.Fetched"/> edit
///   state are included, along with any nodes referenced by dirty ways (needed to render
///   way geometry after restore).
/// </summary>
public class EditBufferDraft
{
    /// <summary>
    ///   The timestamp when the draft was last saved to IndexedDB.
    /// </summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>
    ///   Number of elements with a dirty (non-Fetched) edit state.
    /// </summary>
    public int DirtyCount { get; set; }

    /// <summary>
    ///   Value of <see cref="EditBufferState.NextNegativeId"/> at save time.
    /// </summary>
    public long NextNegativeId { get; set; }

    /// <summary>
    ///   Nodes with dirty edit states, plus nodes referenced by dirty ways.
    /// </summary>
    public List<DraftNode> Nodes { get; set; } = [];

    /// <summary>
    ///   Ways with dirty edit states.
    /// </summary>
    public List<DraftWay> Ways { get; set; } = [];

    /// <summary>
    ///   Relations with dirty edit states.
    /// </summary>
    public List<DraftRelation> Relations { get; set; } = [];

    /// <summary>
    ///   Edit states for dirty elements only (Fetched nodes supporting dirty ways are not included).
    /// </summary>
    public List<DraftEditState> EditStates { get; set; } = [];
}
