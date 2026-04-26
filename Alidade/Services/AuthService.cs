using Alidade.Models.Auth;
using Microsoft.JSInterop;

namespace Alidade.Services;

/// <summary>
///   Implements the OAuth 2.0 PKCE flow against the currently active OSM-compatible
///   authorization server. The target server URL is resolved from <see cref="SettingsStateService"/>
///   on each call so that switching endpoints in the UI takes effect immediately.
///   A popup window completes the authorization redirect and postMessages the code back
///   to the main window.
///   <para>
///     All account storage is delegated to <see cref="IndexedDBService"/>. The active
///     account is recorded as a user ID under <c>selected_account:{endpoint}</c> and
///     resolved to a token at read time from the <c>auth_accounts:{endpoint}</c> array.
///   </para>
///   <para>
///     The HTTP token exchange and user-info fetch are delegated to
///     <see cref="OsmOAuthClient"/>; this service is responsible for popup management,
///     state dispatch, and account storage only.
///   </para>
/// </summary>
public class AuthService(
    IJSRuntime js,
    OsmOAuthClient oauthClient,
    IndexedDBService storage,
    AuthStateService authState,
    SettingsStateService settingsState,
    SettingsService settingsService,
    ILogger<AuthService> log) : IAsyncDisposable
{
    private ApiEndpointInfo ActiveEndpoint => ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint];
    private string OsmBaseUrl => ActiveEndpoint.OsmBaseUrl;
    private string ClientId => ActiveEndpoint.ClientId;

    private DotNetObjectReference<AuthService>? _selfRef;

    #region Session restore

    /// <summary>
    ///   Loads stored accounts for the active endpoint, then attempts to restore the
    ///   previously active session. If only one account is stored it is activated
    ///   automatically; if several exist the account picker is shown.
    /// </summary>
    public async Task TryRestoreSessionAsync()
    {
        ApiEndpoints endpoint = settingsState.State.ActiveEndpoint;
        List<StoredAccount> accounts = await storage.GetStoredAccountsAsync(endpoint);
        authState.SetState(authState.State with { StoredAccounts = accounts });

        if (accounts.Count == 0)
        {
            return;
        }

        string? activeToken = await storage.GetActiveTokenAsync(endpoint);
        StoredAccount? toActivate = string.IsNullOrEmpty(activeToken)
            ? null
            : accounts.Find(a => a.Token == activeToken);

        if (toActivate is not null)
        {
            await ActivateStoredAccountAsync(toActivate);
            return;
        }

        // Auto-select when unambiguous.
        if (accounts.Count == 1)
        {
            await ActivateStoredAccountAsync(accounts[0]);
        }
        else
        {
            authState.SetState(authState.State with { IsAccountSwitcherOpen = true });
        }
    }

    #endregion

    #region Login

    /// <summary>
    ///   Begins the OAuth authorization flow by generating a PKCE challenge and opening
    ///   the OSM authorization popup.
    /// </summary>
    public async Task LoginAsync()
    {
        authState.SetState(authState.State with { IsLoading = true, IsAccountSwitcherOpen = false });

        string verifier = OsmOAuthClient.GenerateCodeVerifier();
        string challenge = OsmOAuthClient.GenerateCodeChallenge(verifier);
        string state = OsmOAuthClient.GenerateState();

        string redirectUri = await GetRedirectUriAsync();
        string authUrl = $"{OsmBaseUrl}/oauth2/authorize"
            + $"?response_type=code"
            + $"&client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&code_challenge={Uri.EscapeDataString(challenge)}"
            + $"&code_challenge_method=S256"
            + $"&scope=read_prefs%20write_api"
            + $"&state={Uri.EscapeDataString(state)}";

        _selfRef = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("authInterop.openLoginPopup", authUrl, _selfRef, verifier, state);
    }

    /// <summary>
    ///   JS-invokable callback called by the OAuth popup relay script when the authorization
    ///   server redirects back with an authorization code.
    /// </summary>
    [JSInvokable]
    public async Task OnOAuthCallbackAsync(string code, string verifier)
    {
        try
        {
            ApiEndpoints endpoint = settingsState.State.ActiveEndpoint;
            string redirectUri = await GetRedirectUriAsync();
            string tokenEndpoint = $"{OsmBaseUrl}/oauth2/token";
            string token = await oauthClient.ExchangeCodeAsync(tokenEndpoint, ClientId, code, verifier, redirectUri);

            // Fetch user info before storing
            string userInfoUrl = $"{ActiveEndpoint.ApiBase}/user/details.json";
            await FetchAndDispatchUserInfoAsync(token, userInfoUrl);
            if (!authState.State.IsLoggedIn)
            {
                return;
            }

            OsmUserInfo user = authState.State.User!;
            StoredAccount newAccount = new(user.UserName, user.UserId, token);

            List<StoredAccount> accounts = await storage.GetStoredAccountsAsync(endpoint);
            accounts.RemoveAll(a => a.UserId == newAccount.UserId);
            accounts.Insert(0, newAccount);
            await storage.SaveStoredAccountsAsync(endpoint, accounts);
            await storage.SetSelectedAccountAsync(endpoint, user.UserId);

            authState.SetState(authState.State with { StoredAccounts = accounts, IsAccountSwitcherOpen = false });
            await settingsService.FetchPrefsFromOsmAsync();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "AuthService: OnOAuthCallbackAsync failed");
            authState.SetState(authState.State with { IsLoading = false });
        }
    }

    #endregion

    #region Logout

    /// <summary>
    ///   Removes the current account from the stored accounts list for this endpoint
    ///   and clears the active selection. Other stored accounts are preserved.
    /// </summary>
    public async Task LogoutAsync()
    {
        OsmUserInfo? currentUser = authState.State.User;
        if (currentUser is null)
        {
            return;
        }

        ApiEndpoints endpoint = settingsState.State.ActiveEndpoint;
        List<StoredAccount> accounts = await storage.GetStoredAccountsAsync(endpoint);
        accounts.RemoveAll(a => a.UserId == currentUser.UserId);
        await storage.SaveStoredAccountsAsync(endpoint, accounts);
        await storage.ClearSelectedAccountAsync(endpoint);

        authState.SetState(authState.State with
        {
            User = null,
            IsLoading = false,
            StoredAccounts = accounts,
            PendingAccountSwitch = null
        });
    }

    #endregion

    #region Endpoint switching

    /// <summary>
    ///   Called after an endpoint switch is confirmed. Clears the current session without
    ///   removing any stored accounts, then loads accounts for the new endpoint and
    ///   auto-selects if there is exactly one, or shows the account picker if there are several.
    /// </summary>
    public async Task HandleEndpointSwitchAsync(ApiEndpoints newEndpoint)
    {
        authState.SetState(new AuthState());

        List<StoredAccount> accounts = await storage.GetStoredAccountsAsync(newEndpoint);
        authState.SetState(authState.State with { StoredAccounts = accounts });

        if (accounts.Count == 0)
        {
            return;
        }

        if (accounts.Count == 1)
        {
            await ActivateStoredAccountAsync(accounts[0]);
        }
        else
        {
            authState.SetState(authState.State with { IsAccountSwitcherOpen = true });
        }
    }

    #endregion

    #region Account switching

    /// <summary>
    ///   Switches the active session to a previously stored account, validating the token
    ///   before committing. If the token is expired the account is removed.
    /// </summary>
    public async Task SwitchAccountAsync(StoredAccount account)
    {
        ApiEndpoints endpoint = settingsState.State.ActiveEndpoint;
        await storage.SetSelectedAccountAsync(endpoint, account.UserId);
        string userInfoUrl = $"{ActiveEndpoint.ApiBase}/user/details.json";
        await FetchAndDispatchUserInfoAsync(account.Token, userInfoUrl);

        if (!authState.State.IsLoggedIn)
        {
            return;
        }

        List<StoredAccount> accounts = await storage.GetStoredAccountsAsync(endpoint);
        accounts.RemoveAll(a => a.UserId == account.UserId);
        accounts.Insert(0, account);
        await storage.SaveStoredAccountsAsync(endpoint, accounts);

        authState.SetState(authState.State with
        {
            StoredAccounts = accounts,
            PendingAccountSwitch = null,
            IsAccountSwitcherOpen = false
        });
    }

    /// <summary>
    ///   Stores the desired account as pending, waiting for a dirty-buffer confirmation
    ///   before the switch is applied. Closes the account picker.
    /// </summary>
    public void RequestAccountSwitch(StoredAccount account)
        => authState.SetState(authState.State with
        {
            PendingAccountSwitch = account,
            IsAccountSwitcherOpen = false
        });

    /// <summary>
    ///   Cancels the pending account switch without changing the active session.
    /// </summary>
    public void CancelAccountSwitch()
        => authState.SetState(authState.State with { PendingAccountSwitch = null });

    /// <summary>
    ///   Opens the account picker dialog.
    /// </summary>
    public void OpenAccountSwitcher()
        => authState.SetState(authState.State with { IsAccountSwitcherOpen = true });

    /// <summary>
    ///   Closes the account picker dialog.
    /// </summary>
    public void CloseAccountSwitcher()
        => authState.SetState(authState.State with { IsAccountSwitcherOpen = false });

    #endregion

    #region Public helpers

    /// <summary>
    ///   Returns the bearer token for the currently selected account on the active endpoint,
    ///   or <see langword="null"/> if no account is selected.
    /// </summary>
    /// <returns>The bearer token, or null.</returns>
    public Task<string?> GetTokenAsync()
        => storage.GetActiveTokenAsync(settingsState.State.ActiveEndpoint);

    #endregion

    #region IAsyncDisposable

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _selfRef?.Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion

    #region Private helpers

    private async Task ActivateStoredAccountAsync(StoredAccount account)
    {
        ApiEndpoints endpoint = settingsState.State.ActiveEndpoint;
        await storage.SetSelectedAccountAsync(endpoint, account.UserId);
        string userInfoUrl = $"{ActiveEndpoint.ApiBase}/user/details.json";
        await FetchAndDispatchUserInfoAsync(account.Token, userInfoUrl);

        if (authState.State.IsLoggedIn)
        {
            return;
        }

        // Token expired
        List<StoredAccount> remaining = await storage.GetStoredAccountsAsync(endpoint);
        remaining.RemoveAll(a => a.UserId == account.UserId);
        await storage.SaveStoredAccountsAsync(endpoint, remaining);
        await storage.ClearSelectedAccountAsync(endpoint);
        authState.SetState(authState.State with { StoredAccounts = remaining });

        if (remaining.Count == 1)
        {
            await ActivateStoredAccountAsync(remaining[0]);
        }
        else if (remaining.Count > 1)
        {
            authState.SetState(authState.State with { IsAccountSwitcherOpen = true });
        }
    }

    private async Task FetchAndDispatchUserInfoAsync(string token, string userInfoUrl)
    {
        OsmUserInfo? info = await oauthClient.FetchUserInfoAsync(userInfoUrl, token);
        if (info is null)
        {
            return; // Caller checks authState.State.IsLoggedIn and handles cleanup.
        }

        authState.SetState(authState.State with { User = info, IsLoading = false });
    }

    private async Task<string> GetRedirectUriAsync()
    {
        string origin = await js.InvokeAsync<string>("eval", "window.location.origin");
        return $"{origin}/oauth-callback.html";
    }

    #endregion
}
