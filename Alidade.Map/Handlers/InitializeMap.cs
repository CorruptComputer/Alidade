namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class InitializeMap(MapInteropService map) : IRequestHandler<InitializeMap.Command, CommandResult>
{
    /// <summary>
    ///   Initializes the MapLibre GL map inside the named container element.
    /// </summary>
    /// <param name="ContainerId">The HTML element ID of the map container div.</param>
    public record Command(string ContainerId) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.InitializeAsync(request.ContainerId);
        return CommandResult.Pass();
    }
}
