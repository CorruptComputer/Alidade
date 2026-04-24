namespace Alidade.Osm.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="EditBufferState"/>.
/// </summary>
public class EditBufferStateService
{
    /// <summary>
    ///   The current edit buffer state.
    /// </summary>
    public EditBufferState State { get; private set; } = new();

    /// <summary>
    ///   Raised after State is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    ///   Replaces the current state with the provided new state, and raises StateChanged to notify subscribers of the update.
    /// </summary>
    /// <param name="state">The new edit buffer state.</param>
    public void SetState(EditBufferState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
