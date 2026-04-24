namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class SetLayerVisibility(MapInteropService map) : IRequestHandler<SetLayerVisibility.Command, CommandResult>
{
    /// <summary>
    ///   Shows or hides the named MapLibre layer.
    /// </summary>
    /// <param name="LayerId">The MapLibre layer ID.</param>
    /// <param name="Visible">Whether the layer should be visible.</param>
    public record Command(string LayerId, bool Visible) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.SetLayerVisibilityAsync(request.LayerId, request.Visible);
        return CommandResult.Pass();
    }
}
