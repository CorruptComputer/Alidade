namespace Alidade.Handlers.Settings;

/// <inheritdoc />
public class ConfirmEndpointSwitch(SettingsService settings, AuthService auth, IMediator mediator)
    : IRequestHandler<ConfirmEndpointSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Activates the new endpoint, clears the edit buffer and selection, saves settings,
    ///   and transitions the auth session to the new endpoint (auto-selecting if one account
    ///   is stored there, or showing the account picker if several exist).
    /// </summary>
    public record Command(ApiEndpoints Target) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        settings.ApplyEndpointSwitch(request.Target);
        await mediator.Send(new EditBuffer.ClearEditBuffer.Command(), cancellationToken);
        await mediator.Send(new Selection.ClearSelection.Command(), cancellationToken);
        await settings.SaveToStorageAsync();
        await auth.HandleEndpointSwitchAsync(request.Target);
        await settings.FetchPrefsFromOsmAsync();
        return CommandResult.Pass();
    }
}
