namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class AppendWayPoint(ToolStateService toolState) : IRequestHandler<AppendWayPoint.Command, CommandResult>
{
    /// <summary>
    ///   Adds a new lat/lon vertex to the in-progress way.
    /// </summary>
    public record Command(double Lat, double Lon, long? ExistingNodeId) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        toolState.SetState(toolState.State with
        {
            WayInProgress = toolState.State.WayInProgress.Add((request.Lat, request.Lon, request.ExistingNodeId))
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
