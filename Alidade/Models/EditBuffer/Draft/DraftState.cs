
namespace Alidade.Models.EditBuffer.Draft;

/// <summary>
///   Tracks whether a recoverable draft of unsaved changes exists in IndexedDB.
///   When <see cref="HasDraft"/> is true the recovery dialog is shown,
///   prompting the user to continue or discard the previous session's edits.
/// </summary>
/// <param name="HasDraft">True if a recoverable draft exists in IndexedDB.</param>
/// <param name="DirtyCount">The number of unsaved changes in the current draft.</param>
/// <param name="SavedAt">The timestamp when the draft was last saved to IndexedDB.</param>
public record DraftState(bool HasDraft, int? DirtyCount, DateTimeOffset? SavedAt)
{
    /// <summary>
    ///   Initializes with no draft pending.
    /// </summary>
    public DraftState() : this(false, null, null) { }
}
