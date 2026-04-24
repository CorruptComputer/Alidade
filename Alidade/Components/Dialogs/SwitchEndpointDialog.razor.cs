using Alidade.Handlers.Map;
using Alidade.Handlers.Settings;

namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog shown when the user attempts to switch API endpoints with a non-empty edit buffer.
/// </summary>
public partial class SwitchEndpointDialog(SettingsStateService settingsState, IMediator mediator) : IDisposable
{
    private EndpointState ActiveEndpointState
        => ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].State;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        settingsState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void Upload()
    {
        // Open the upload dialog. ApplyDiffResult handler detects the pending switch
        // and auto-confirms via ConfirmEndpointSwitch once the buffer is clean.
        _ = mediator.Send(new ToggleUploadDialog.Command());

        // Cancel the pending endpoint switch
        _ = mediator.Send(new CancelEndpointSwitch.Command());
    }

    private void Discard(ApiEndpoints pending)
    {
        _ = mediator.Send(new ClearEditBuffer.Command());
        // ConfirmEndpointSwitch handles the auth transition
        _ = mediator.Send(new ConfirmEndpointSwitch.Command(pending));
    }

    private void Cancel()
        => _ = mediator.Send(new CancelEndpointSwitch.Command());

    /// <inheritdoc />
    public void Dispose()
        => settingsState.StateChanged -= OnStateChanged;
}
