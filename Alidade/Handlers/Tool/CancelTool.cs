namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class CancelTool(ToolStateService toolState) : IRequestHandler<CancelTool.Command, CommandResult>
{
    /// <summary>
    ///   Returns to the Select tool and clears in-progress state.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        toolState.SetState(toolState.State with
        {
            Active = ActiveTools.Select,
            WayInProgress = [],
            SnapTargetNodeId = null
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
