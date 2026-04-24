namespace Alidade.Core.Services;

/// <summary>
///   Singleton state service holding the current <see cref="SettingsState"/>.
/// </summary>
public sealed class SettingsStateService
{
    /// <summary>
    ///   The current settings state.
    /// </summary>
    public SettingsState State { get; private set; } = new();

    /// <summary>
    ///   Raised after the settings state is replaced.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    ///   Returns the currently active API endpoint.
    /// </summary>
    /// <returns>The active <see cref="ApiEndpoints"/> value.</returns>
    public ApiEndpoints GetActiveEndpoint() => State.ActiveEndpoint;

    /// <summary>
    ///   An optional API endpoint that the user has selected but not yet confirmed.
    ///   Non-null only while the edit buffer is dirty and a switch is pending.
    /// </summary>
    public ApiEndpoints? PendingEndpoint => State.PendingEndpoint;

    /// <summary>
    ///   Replaces the current <see cref="State"/> with the provided new state, and raises <see cref="StateChanged"/> to notify subscribers of the update.
    /// </summary>
    /// <param name="state"></param>
    public void SetState(SettingsState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
