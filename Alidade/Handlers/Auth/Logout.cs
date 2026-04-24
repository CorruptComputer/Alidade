namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class Logout(AuthService auth) : IRequestHandler<Logout.Command, CommandResult>
{
    /// <summary>
    ///   Removes the stored token and clears auth state.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync();
        return CommandResult.Pass();
    }
}
