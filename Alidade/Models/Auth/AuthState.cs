namespace Alidade.Models.Auth;

/// <summary>
///   Tracks the current authentication state: the logged-in user's profile, whether
///   an OAuth flow is in progress, all stored accounts for the active endpoint, and
///   any pending account switch waiting for a dirty-buffer confirmation.
/// </summary>
/// <param name="User">The logged-in user's OSM profile, or null if not logged in.</param>
/// <param name="IsLoading">Indicates whether an OAuth flow is currently in progress.</param>
/// <param name="StoredAccounts">All accounts saved for the currently active endpoint.</param>
/// <param name="PendingAccountSwitch">An account the user wants to switch to, held pending dirty-buffer confirmation.</param>
/// <param name="IsAccountSwitcherOpen">Whether the account picker dialog is visible.</param>
public record AuthState(
    OsmUserInfo? User,
    bool IsLoading,
    IReadOnlyList<StoredAccount> StoredAccounts,
    StoredAccount? PendingAccountSwitch,
    bool IsAccountSwitcherOpen)
{
    /// <summary>
    ///   Initializes with no user, no login in progress, no stored accounts, and no pending switch.
    /// </summary>
    public AuthState() : this(null, false, [], null, false) { }

    /// <summary>
    ///   Returns <see langword="true"/> when a user profile is present.
    /// </summary>
    public bool IsLoggedIn
        => User is not null;
}
