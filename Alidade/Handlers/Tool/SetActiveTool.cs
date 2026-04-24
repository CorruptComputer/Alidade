using Alidade.Map.Handlers;

namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class SetActiveTool(ToolStateService toolState, IMediator mediator) : IRequestHandler<SetActiveTool.Command, CommandResult>
{
    /// <summary>
    ///   Switches to the requested tool, resets in-progress way state, and syncs the
    ///   MapLibre cursor/interaction mode.
    /// </summary>
    public record Command(ActiveTools Tool) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        toolState.SetState(toolState.State with
        {
            Active = request.Tool,
            WayInProgress = [],
            SnapTargetNodeId = null
        });

        string mapTool = request.Tool switch
        {
            ActiveTools.DrawNode => "drawNode",
            ActiveTools.DrawWay  => "drawWay",
            ActiveTools.DrawArea => "drawArea",
            _                    => "select"
        };

        await mediator.Send(new SyncMapActiveTool.Command(mapTool), cancellationToken);
        return CommandResult.Pass();
    }
}
