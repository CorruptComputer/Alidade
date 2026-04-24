namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class RequestAccountSwitch(AuthService auth) : IRequestHandler<RequestAccountSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Stores the desired account as pending (shown in the dirty-buffer confirmation dialog).
    /// </summary>
    public record Command(StoredAccount Account) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        auth.RequestAccountSwitch(request.Account);
        return Task.FromResult(CommandResult.Pass());
    }
}
