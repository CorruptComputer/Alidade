using Alidade.Models.Auth;

namespace Alidade.Services.State;

/// <summary>
///   Singleton state service holding the current <see cref="AuthState"/>.
/// </summary>
public class AuthStateService
{
    /// <summary>
    ///   The current auth state.
    /// </summary>
    public AuthState State { get; private set; } = new();

    /// <summary>
    ///   Raised after <see cref="State"/> is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    internal void SetState(AuthState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
