using Alidade.Handlers.Auth;
using Alidade.Handlers.Map;

namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog shown when the user attempts to switch accounts while the edit buffer is dirty.
/// </summary>
public partial class SwitchAccountDialog(AuthStateService authState, SettingsStateService settingsState, IMediator mediator)
    : IDialog, IDisposable
{
    /// <inheritdoc/>
    public static string Title => "Unsaved Changes";

    private EndpointState ActiveEndpointState
        => ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].State;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        authState.StateChanged += OnStateChanged;
        settingsState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void Upload()
    {
        // Open the upload dialog; once the buffer is clean the user can retry the switch.
        _ = mediator.Send(new ToggleUploadDialog.Command());

        // Cancel the pending account switch
        _ = mediator.Send(new CancelAccountSwitch.Command());
    }

    private void Discard(StoredAccount pending)
    {
        _ = mediator.Send(new ConfirmAccountSwitch.Command(pending));
    }

    private void Cancel()
        => _ = mediator.Send(new CancelAccountSwitch.Command());

    /// <inheritdoc />
    public void Dispose()
    {
        authState.StateChanged -= OnStateChanged;
        settingsState.StateChanged -= OnStateChanged;
    }
}
