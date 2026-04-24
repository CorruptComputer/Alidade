namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="ValidationState"/>.
/// </summary>
public class ValidationStateService
{
    /// <summary>
    ///   The current validation state.
    /// </summary>
    public ValidationState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(ValidationState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
