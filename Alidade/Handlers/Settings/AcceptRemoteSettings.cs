namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class AcceptRemoteSettings(SettingsService settings) : IRequestHandler<AcceptRemoteSettings.Command, CommandResult>
{
    /// <summary>
    ///   Applies the conflicting remote keybindings as the new local state and saves to IndexedDB.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await settings.AcceptRemoteSettingsAsync();
        return CommandResult.Pass();
    }
}
