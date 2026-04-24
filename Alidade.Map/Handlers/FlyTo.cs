namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class FlyTo(MapInteropService map) : IRequestHandler<FlyTo.Command, CommandResult>
{
    /// <summary>
    ///   Animates the viewport to the given coordinates at the specified zoom level.
    /// </summary>
    /// <param name="Lat">Target latitude in decimal degrees.</param>
    /// <param name="Lon">Target longitude in decimal degrees.</param>
    /// <param name="Zoom">Target MapLibre zoom level.</param>
    public record Command(double Lat, double Lon, int Zoom) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.FlyToAsync(request.Lat, request.Lon, request.Zoom);
        return CommandResult.Pass();
    }
}
