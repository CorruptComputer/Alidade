namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class BeginLogin(AuthService auth) : IRequestHandler<BeginLogin.Command, CommandResult>
{
    /// <summary>
    ///   Begins the OAuth PKCE login flow.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await auth.LoginAsync();
        return CommandResult.Pass();
    }
}
