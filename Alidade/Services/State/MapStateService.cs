namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="MapState"/>.
///   Raise <see cref="StateChanged"/> whenever the state changes.
/// </summary>
public class MapStateService
{
    /// <summary>
    ///   The current map state.
    /// </summary>
    public MapState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(MapState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
