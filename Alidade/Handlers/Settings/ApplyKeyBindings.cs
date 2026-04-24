namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class ApplyKeyBindings(SettingsStateService settingsState, SettingsService settings)
    : IRequestHandler<ApplyKeyBindings.Command, CommandResult>
{
    /// <summary>
    ///   Wholesale-replaces the keybinding overrides (used when loading from storage/OSM prefs).
    /// </summary>
    public record Command(Dictionary<string, string> Overrides) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settingsState.SetState(settingsState.State with
        {
            KeyBindings = KeyBindingsConfig.FromOverrides(request.Overrides)
        });

        await settings.SaveToStorageAsync();
        return CommandResult.Pass();
    }
}
