namespace Alidade.Handlers.Map;

/// <inheritdoc />
public sealed class ToggleCircularizeDialog(MapStateService mapState)
    : IRequestHandler<ToggleCircularizeDialog.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the circularize panel.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { CircularizeDialogVisible = !mapState.State.CircularizeDialogVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
