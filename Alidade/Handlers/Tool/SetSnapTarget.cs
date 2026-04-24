namespace Alidade.Handlers.Tool;

/// <inheritdoc />
public class SetSnapTarget(ToolStateService toolState) : IRequestHandler<SetSnapTarget.Command, CommandResult>
{
    /// <summary>
    ///   Updates the snap-target node.
    /// </summary>
    public record Command(long? NodeId) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        toolState.SetState(toolState.State with { SnapTargetNodeId = request.NodeId });
        return Task.FromResult(CommandResult.Pass());
    }
}
