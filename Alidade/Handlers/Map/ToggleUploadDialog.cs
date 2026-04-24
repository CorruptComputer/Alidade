namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class ToggleUploadDialog(MapStateService mapState) : IRequestHandler<ToggleUploadDialog.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the changeset upload dialog.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { UploadDialogVisible = !mapState.State.UploadDialogVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
