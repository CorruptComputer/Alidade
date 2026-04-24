namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog shown when the user attempts to switch API endpoints with a non-empty edit buffer.
/// </summary>
public partial class SwitchEndpointDialog(SettingsStateService settingsState, IMediator mediator) : IDisposable
{
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
        _ = mediator.Send(new Handlers.Map.ToggleUploadDialog.Command());
    }

    private void Discard(ApiEndpoints pending)
    {
        _ = mediator.Send(new Handlers.EditBuffer.ClearEditBuffer.Command());
        // ConfirmEndpointSwitch handles the auth transition
        _ = mediator.Send(new Handlers.Settings.ConfirmEndpointSwitch.Command(pending));
    }

    private void Cancel()
        => _ = mediator.Send(new Handlers.Settings.CancelEndpointSwitch.Command());

    /// <inheritdoc />
    public void Dispose()
        => settingsState.StateChanged -= OnStateChanged;
}
