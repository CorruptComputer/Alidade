namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class ResetAllKeyBindings(SettingsService settings) : IRequestHandler<ResetAllKeyBindings.Command, CommandResult>
{
    /// <summary>
    ///   Resets all keybindings to catalog defaults, then saves and pushes.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settings.ResetAllKeyBindings();
        await settings.SaveAndPushAsync();
        return CommandResult.Pass();
    }
}
