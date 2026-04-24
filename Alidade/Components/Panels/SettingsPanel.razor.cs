using Alidade.Handlers.Auth;
using Alidade.Handlers.Map;
using Alidade.Handlers.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Alidade.Components.Panels;

/// <summary>
///   Panel for configuring editor settings such as the active API endpoint and keybindings.
/// </summary>
public partial class SettingsPanel(
    MapStateService mapState,
    AuthStateService authState,
    SettingsStateService settingsState,
    EditBufferStateService editBufferState,
    IMediator mediator,
    SettingsService settingsService) : IDisposable
{
    private string? _capturingActionId;
    private string? _conflictActionId;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        mapState.StateChanged += OnStateChanged;
        authState.StateChanged += OnStateChanged;
        settingsState.StateChanged += OnStateChanged;
        editBufferState.StateChanged += OnStateChanged;
        settingsService.StatusChanged += OnStatusChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();
    private void OnStatusChanged() => InvokeAsync(StateHasChanged);

    #region Endpoint

    private void Close()
        => _ = mediator.Send(new ToggleSettingsPanel.Command());

    private void OnEndpointChanged(ChangeEventArgs e)
    {
        if (!Enum.TryParse(e.Value?.ToString(), out ApiEndpoints selected)) return;
        if (selected == settingsState.State.ActiveEndpoint) return;
        if (ApiEndpointCatalog.Endpoints[selected].State == EndpointState.Disabled) return;

        _ = mediator.Send(new ToggleSettingsPanel.Command());

        if (editBufferState.State.IsDirty)
        {
            _ = mediator.Send(new RequestEndpointSwitch.Command(selected));
        }
        else
        {
            // ConfirmEndpointSwitch handles the auth transition for the new endpoint.
            _ = mediator.Send(new ConfirmEndpointSwitch.Command(selected));
        }
    }

    private void OpenAccountSwitcher()
        => _ = mediator.Send(new OpenAccountSwitcher.Command());

    #endregion

    #region Conflict resolution

    private void AcceptLocal()
        => _ = mediator.Send(new AcceptLocalSettings.Command());

    private void AcceptRemote()
        => _ = mediator.Send(new AcceptRemoteSettings.Command());

    #endregion

    #region Keybindings

    private void StartCapture(string actionId)
    {
        _capturingActionId = actionId;
        _conflictActionId = null;
    }

    private void CancelCapture()
    {
        _capturingActionId = null;
        _conflictActionId = null;
    }

    private void OnCaptureKeyDown(KeyboardEventArgs e)
    {
        if (_capturingActionId is null) return;

        if (e.Key == "Escape")
        {
            CancelCapture();
            return;
        }

        string? combo = KeyBindingCatalog.NormalizeCombo(e.CtrlKey, e.AltKey, e.ShiftKey, e.Key);
        if (string.IsNullOrEmpty(combo))
        {
            return;
        }

        if (settingsState.State.KeyBindings.IsComboTaken(combo, _capturingActionId))
        {
            _conflictActionId = settingsState.State.KeyBindings.FindAction(combo);
        }

        _ = mediator.Send(new UpdateKeyBinding.Command(_capturingActionId, combo));
        _capturingActionId = null;
        _conflictActionId = null;
    }

    private void ResetBinding(string actionId)
        => _ = mediator.Send(new UpdateKeyBinding.Command(actionId, string.Empty));

    private void ResetAllBindings()
        => _ = mediator.Send(new ResetAllKeyBindings.Command());

    #endregion

    /// <inheritdoc />
    public void Dispose()
    {
        mapState.StateChanged -= OnStateChanged;
        authState.StateChanged -= OnStateChanged;
        settingsState.StateChanged -= OnStateChanged;
        editBufferState.StateChanged -= OnStateChanged;
        settingsService.StatusChanged -= OnStatusChanged;
    }
}
