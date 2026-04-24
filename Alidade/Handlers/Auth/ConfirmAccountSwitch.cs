namespace Alidade.Handlers.Auth;

/// <inheritdoc />
public class ConfirmAccountSwitch(AuthService auth, IMediator mediator)
    : IRequestHandler<ConfirmAccountSwitch.Command, CommandResult>
{
    /// <summary>
    ///   Switches the active session to the specified account, clears the edit buffer
    ///   and selection, then fetches preferences from the server.
    /// </summary>
    public record Command(StoredAccount Account) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await mediator.Send(new EditBuffer.ClearEditBuffer.Command(), cancellationToken);
        await mediator.Send(new Selection.ClearSelection.Command(), cancellationToken);
        await auth.SwitchAccountAsync(request.Account);
        return CommandResult.Pass();
    }
}
