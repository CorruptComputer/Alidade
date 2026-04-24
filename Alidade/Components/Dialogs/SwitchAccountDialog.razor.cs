namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog shown when the user attempts to switch accounts while the edit buffer is dirty.
/// </summary>
public partial class SwitchAccountDialog(AuthStateService authState, IMediator mediator) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        authState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void Upload()
    {
        // Open the upload dialog; once the buffer is clean the user can retry the switch.
        _ = mediator.Send(new Handlers.Map.ToggleUploadDialog.Command());
    }

    private void Discard(StoredAccount pending)
    {
        _ = mediator.Send(new Handlers.Auth.ConfirmAccountSwitch.Command(pending));
    }

    private void Cancel()
        => _ = mediator.Send(new Handlers.Auth.CancelAccountSwitch.Command());

    /// <inheritdoc />
    public void Dispose()
        => authState.StateChanged -= OnStateChanged;
}
