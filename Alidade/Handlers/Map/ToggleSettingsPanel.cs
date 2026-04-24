namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class ToggleSettingsPanel(MapStateService mapState) : IRequestHandler<ToggleSettingsPanel.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the settings panel.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { SettingsPanelVisible = !mapState.State.SettingsPanelVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
