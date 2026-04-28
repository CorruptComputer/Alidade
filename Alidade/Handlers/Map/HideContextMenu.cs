namespace Alidade.Handlers.Map;

/// <inheritdoc />
public sealed class HideContextMenu(MapStateService mapState)
    : IRequestHandler<HideContextMenu.Command, CommandResult>
{
    /// <summary>
    ///   Hides the right-click context menu and clears its target element.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with
        {
            ContextMenuVisible = false,
            ContextMenuTargetElement = null
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
