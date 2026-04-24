namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class ClearWayInProgress(ToolStateService toolState) : IRequestHandler<ClearWayInProgress.Command, CommandResult>
{
    /// <summary>
    ///   Removes all accumulated vertices from the in-progress way.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        toolState.SetState(toolState.State with { WayInProgress = [] });
        return Task.FromResult(CommandResult.Pass());
    }
}
