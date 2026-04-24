namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class CloseAccountSwitcher(AuthService auth) : IRequestHandler<CloseAccountSwitcher.Command, CommandResult>
{
    /// <summary>
    ///   Closes the account picker dialog.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        auth.CloseAccountSwitcher();
        return Task.FromResult(CommandResult.Pass());
    }
}
