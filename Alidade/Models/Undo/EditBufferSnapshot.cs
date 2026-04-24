namespace Alidade.Models.Undo;

/// <summary>
///   Snapshot-based <see cref="IEditAction"/> that stores the complete
///   <see cref="EditBufferState"/> before and after a single user edit.
/// </summary>
public sealed class EditBufferSnapshot : IEditAction
{
    private readonly EditBufferState _before;
    private readonly EditBufferState _after;

    /// <summary>
    ///   Initializes a new snapshot undo entry.
    /// </summary>
    /// <param name="description">Human-readable label shown in the undo history panel.</param>
    /// <param name="before">The edit buffer state immediately before the action was applied.</param>
    /// <param name="after">The edit buffer state immediately after the action was applied.</param>
    public EditBufferSnapshot(string description, EditBufferState before, EditBufferState after)
    {
        Description = description;
        _before = before;
        _after = after;
    }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public EditBufferState Apply(EditBufferState state) => _after;

    /// <inheritdoc />
    public EditBufferState Reverse(EditBufferState state) => _before;
}
