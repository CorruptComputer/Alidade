namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class RequestEndpointSwitch(SettingsService settings) : IRequestHandler<RequestEndpointSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Stores the desired endpoint as pending, showing the confirmation dialog.
    /// </summary>
    public record Command(ApiEndpoints Target) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settings.RequestEndpointSwitch(request.Target);
        return Task.FromResult(CommandResult.Pass());
    }
}
