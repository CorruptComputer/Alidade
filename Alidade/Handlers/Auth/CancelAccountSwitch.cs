namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class CancelAccountSwitch(AuthService auth) : IRequestHandler<CancelAccountSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Cancels the pending account switch without changing the active session.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        auth.CancelAccountSwitch();
        return Task.FromResult(CommandResult.Pass());
    }
}
