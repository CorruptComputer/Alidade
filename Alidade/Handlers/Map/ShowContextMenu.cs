namespace Alidade.Handlers.Map;

/// <inheritdoc />
public sealed class ShowContextMenu(MapStateService mapState)
    : IRequestHandler<ShowContextMenu.Command, CommandResult>
{
    /// <summary>
    ///   Shows the right-click context menu at the given screen position for the specified element.
    /// </summary>
    /// <param name="X">The X position in CSS pixels where the menu should appear.</param>
    /// <param name="Y">The Y position in CSS pixels where the menu should appear.</param>
    /// <param name="Target">The OSM element the menu will act on.</param>
    public record Command(double X, double Y, OsmElementRef Target) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with
        {
            ContextMenuVisible = true,
            ContextMenuX = request.X,
            ContextMenuY = request.Y,
            ContextMenuTargetElement = request.Target
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
