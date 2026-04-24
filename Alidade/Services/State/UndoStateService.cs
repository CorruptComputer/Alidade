using Alidade.Models.Undo;

namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="UndoState"/>
///   and providing Push/Pop helpers used by the undo pipeline behavior and handlers.
/// </summary>
public class UndoStateService
{
    /// <summary>
    ///   The current undo state.
    /// </summary>
    public UndoState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(UndoState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///   Pushes a snapshot onto the undo stack, trimming oldest when at max depth,
    ///   and clears the redo stack.
    /// </summary>
    public void Push(Alidade.Models.Undo.EditBufferSnapshot snapshot)
    {
        ImmutableList<IEditAction> stack = State.UndoStack.Add(snapshot);
        if (stack.Count > UndoState.MaxDepth)
        {
            stack = stack.RemoveAt(0);
        }

        SetState(State with { UndoStack = stack, RedoStack = State.RedoStack.Clear() });
    }

    /// <summary>
    ///   Pops the top entry from the undo stack onto the redo stack.
    ///   Returns the popped action, or null if the stack was empty.
    /// </summary>
    public IEditAction? PopUndo()
    {
        if (State.UndoStack.IsEmpty)
        {
            return null;
        }

        IEditAction action = State.UndoStack[^1];
        SetState(State with
        {
            UndoStack = State.UndoStack.RemoveAt(State.UndoStack.Count - 1),
            RedoStack = State.RedoStack.Add(action)
        });
        return action;
    }

    /// <summary>
    ///   Pops the top entry from the redo stack back onto the undo stack.
    ///   Returns the popped action, or null if the stack was empty.
    /// </summary>
    public IEditAction? PopRedo()
    {
        if (State.RedoStack.IsEmpty)
        {
            return null;
        }

        IEditAction action = State.RedoStack[^1];
        SetState(State with
        {
            RedoStack = State.RedoStack.RemoveAt(State.RedoStack.Count - 1),
            UndoStack = State.UndoStack.Add(action)
        });
        return action;
    }

    /// <summary>
    ///   Clears both stacks (e.g. on endpoint switch or buffer reset).
    /// </summary>
    public void Clear()
        => SetState(State with { UndoStack = [], RedoStack = [] });

    /// <summary>
    ///   Toggles the undo history panel visibility.
    /// </summary>
    public void TogglePanelVisible()
        => SetState(State with { PanelVisible = !State.PanelVisible });
}
