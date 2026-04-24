namespace Alidade.Components.Panels;

/// <summary>
///   Main toolbar containing tool selection buttons, upload, and auth controls.
/// </summary>
public partial class Toolbar(
    ToolStateService toolState,
    AuthStateService authState,
    EditBufferStateService editBufferState,
    SettingsStateService settingsState,
    ValidationStateService validationState,
    IMediator mediator,
    AuthService auth) : IDisposable
{
    private EndpointState ActiveEndpointState
        => ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].State;

    private string ActiveClass(ActiveTools tool)
        => toolState.State.Active == tool ? "toolbar-btn-active" : string.Empty;

    private int ErrorCount
        => validationState.State.Issues.Count(i => i.Severity == ValidationSeverities.Error);

    private int WarnCount
        => validationState.State.Issues.Count(i => i.Severity == ValidationSeverities.Warning);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        toolState.StateChanged += OnStateChanged;
        authState.StateChanged += OnStateChanged;
        editBufferState.StateChanged += OnStateChanged;
        settingsState.StateChanged += OnStateChanged;
        validationState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void SetTool(ActiveTools tool)
        => _ = mediator.Send(new Handlers.Tool.SetActiveTool.Command(tool));

    private void OpenUpload()
        => _ = mediator.Send(new Handlers.Map.ToggleUploadDialog.Command());

    private void ToggleBackground()
        => _ = mediator.Send(new Handlers.Map.ToggleBackgroundPanel.Command());

    private void ToggleSettings()
        => _ = mediator.Send(new Handlers.Map.ToggleSettingsPanel.Command());

    private void ToggleValidation()
        => _ = mediator.Send(new Handlers.Validation.ToggleValidationPanel.Command());

    private async Task LogIn()
        => await auth.LoginAsync();

    private async Task LogOut()
        => await auth.LogoutAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        toolState.StateChanged -= OnStateChanged;
        authState.StateChanged -= OnStateChanged;
        editBufferState.StateChanged -= OnStateChanged;
        settingsState.StateChanged -= OnStateChanged;
        validationState.StateChanged -= OnStateChanged;
    }
}
