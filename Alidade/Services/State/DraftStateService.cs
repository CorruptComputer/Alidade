namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="DraftState"/>.
/// </summary>
public class DraftStateService
{
    /// <summary>
    ///   The current draft state.
    /// </summary>
    public DraftState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(DraftState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
