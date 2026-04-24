namespace Alidade.Models.Undo;

/// <summary>
///   Represents a reversible edit operation that can be pushed onto the undo stack.
/// </summary>
public interface IEditAction
{
    /// <summary>
    ///   A short human-readable description shown in the undo history panel.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///   Returns the edit buffer state that results from applying this action forward.
    /// </summary>
    EditBufferState Apply(EditBufferState state);

    /// <summary>
    ///   Returns the edit buffer state that results from reversing this action.
    /// </summary>
    EditBufferState Reverse(EditBufferState state);
}
