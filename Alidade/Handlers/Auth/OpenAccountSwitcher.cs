namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class OpenAccountSwitcher(AuthService auth) : IRequestHandler<OpenAccountSwitcher.Command, CommandResult>
{
    /// <summary>
    ///   Opens the account picker dialog.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        auth.OpenAccountSwitcher();
        return Task.FromResult(CommandResult.Pass());
    }
}
