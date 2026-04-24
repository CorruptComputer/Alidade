namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class ToggleGridifyDialog(MapStateService mapState) : IRequestHandler<ToggleGridifyDialog.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the gridify dialog.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { GridifyDialogVisible = !mapState.State.GridifyDialogVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
