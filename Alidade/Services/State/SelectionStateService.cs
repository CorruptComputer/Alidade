namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="SelectionState"/>.
/// </summary>
public class SelectionStateService
{
    /// <summary>
    ///   The current selection state.
    /// </summary>
    public SelectionState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(SelectionState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
