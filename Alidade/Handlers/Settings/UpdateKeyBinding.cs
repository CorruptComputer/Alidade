namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class UpdateKeyBinding(SettingsService settings) : IRequestHandler<UpdateKeyBinding.Command, CommandResult>
{
    /// <summary>
    ///   Updates or clears a single keybinding, then saves to IndexedDB and pushes to OSM prefs.
    /// </summary>
    public record Command(string ActionId, string Combo) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settings.UpdateKeyBinding(request.ActionId, request.Combo);
        await settings.SaveAndPushAsync();
        return CommandResult.Pass();
    }
}
