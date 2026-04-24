namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class SyncMapActiveTool(MapInteropService map) : IRequestHandler<SyncMapActiveTool.Command, CommandResult>
{
    /// <summary>
    ///   Sets the active editing tool in MapLibre, controlling cursor and interaction behaviour.
    /// </summary>
    /// <param name="ToolName">
    ///   The tool name expected by <c>mapInterop.setActiveTool</c>
    ///   (e.g. <c>"select"</c>, <c>"drawNode"</c>, <c>"drawWay"</c>, <c>"drawArea"</c>).
    /// </param>
    public record Command(string ToolName) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.SetActiveToolAsync(request.ToolName);
        return CommandResult.Pass();
    }
}
