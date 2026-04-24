namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="ToolState"/>.
/// </summary>
public class ToolStateService
{
    /// <summary>
    ///   The current tool state.
    /// </summary>
    public ToolState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(ToolState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
