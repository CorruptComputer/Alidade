namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog that lists all stored accounts for the active endpoint and lets the user
///   switch between them or initiate a new login.
/// </summary>
public partial class SelectAccountDialog(
    AuthStateService authState,
    EditBufferStateService editBufferState,
    IMediator mediator) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        authState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void Select(StoredAccount account)
    {
        if (editBufferState.State.IsDirty)
        {
            // Store as pending and show the dirty-buffer confirmation dialog.
            _ = mediator.Send(new Handlers.Auth.RequestAccountSwitch.Command(account));
        }
        else
        {
            _ = mediator.Send(new Handlers.Auth.ConfirmAccountSwitch.Command(account));
        }
    }

    private void Login()
    {
        _ = mediator.Send(new Handlers.Auth.CloseAccountSwitcher.Command());
        _ = mediator.Send(new Handlers.Auth.BeginLogin.Command());
    }

    private void Close()
        => _ = mediator.Send(new Handlers.Auth.CloseAccountSwitcher.Command());

    /// <inheritdoc />
    public void Dispose()
        => authState.StateChanged -= OnStateChanged;
}
