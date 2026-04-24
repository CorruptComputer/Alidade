namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public sealed class EndDrawing(
    ToolStateService toolState,
    DrawingToolService drawingService,
    IMediator mediator)
    : IRequestHandler<EndDrawing.Command, CommandResult>
{
    /// <summary>
    ///   Commits the in-progress way or area if it has enough nodes to be valid,
    ///   otherwise cancels the drawing and returns to the Select tool.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        ToolState ts = toolState.State;
        int count = ts.WayInProgress.Count;

        if (ts.Active == ActiveTools.DrawWay && count >= 2)
        {
            await drawingService.CommitCurrentDrawingAsync(isArea: false);
        }
        else if (ts.Active == ActiveTools.DrawArea && count >= 3)
        {
            await drawingService.CommitCurrentDrawingAsync(isArea: true);
        }
        else
        {
            await mediator.Send(new CancelTool.Command(), cancellationToken);
        }

        return CommandResult.Pass();
    }
}
