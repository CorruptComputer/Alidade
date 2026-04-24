namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public sealed class AddNodeToWay(ToolStateService toolState)
    : IRequestHandler<AddNodeToWay.Command, CommandResult>
{
    /// <summary>
    ///   Inserts a node into the in-progress way at a specific index and also appends it
    ///   at the end so that drawing continues from the inserted point. Using the same node
    ///   at two positions creates the shared node required for self-intersecting ways.
    /// </summary>
    public record Command(int InsertIndex, double Lat, double Lon, long NodeId)
        : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        ImmutableList<(double Lat, double Lon, long? NodeId)> wip = toolState.State.WayInProgress
            .Insert(request.InsertIndex, (request.Lat, request.Lon, request.NodeId))
            .Add((request.Lat, request.Lon, request.NodeId));

        toolState.SetState(toolState.State with { WayInProgress = wip });
        return Task.FromResult(CommandResult.Pass());
    }
}
