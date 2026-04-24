namespace Alidade.Models.Undo;

/// <summary>
///   Holds the undo and redo stacks and whether the undo history panel is open.
/// </summary>
public record UndoState(ImmutableList<IEditAction> UndoStack, ImmutableList<IEditAction> RedoStack, bool PanelVisible)
{
    /// <summary>
    ///   Maximum number of actions retained on the undo stack.
    /// </summary>
    public const int MaxDepth = 500;

    /// <summary>
    ///   Initializes an empty undo/redo history with the panel closed.
    /// </summary>
    public UndoState() : this([], [], false) { }
}
