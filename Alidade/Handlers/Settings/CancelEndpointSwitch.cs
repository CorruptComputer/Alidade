namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class CancelEndpointSwitch(SettingsService settings) : IRequestHandler<CancelEndpointSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Clears the pending endpoint switch without changing the active one.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settings.CancelEndpointSwitch();
        return Task.FromResult(CommandResult.Pass());
    }
}
