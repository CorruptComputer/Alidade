namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class AcceptLocalSettings(SettingsService settings) : IRequestHandler<AcceptLocalSettings.Command, CommandResult>
{
    /// <summary>
    ///   Dismisses the remote keybinding conflict, keeping local settings,
    ///   and pushes them to OSM to overwrite the remote.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await settings.AcceptLocalSettingsAsync();
        return CommandResult.Pass();
    }
}
