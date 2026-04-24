namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class PanTo(MapInteropService map) : IRequestHandler<PanTo.Command, CommandResult>
{
    /// <summary>
    ///   Pans the viewport to the given coordinates without changing the zoom level.
    /// </summary>
    /// <param name="Lat">Target latitude in decimal degrees.</param>
    /// <param name="Lon">Target longitude in decimal degrees.</param>
    public record Command(double Lat, double Lon) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.PanToAsync(request.Lat, request.Lon);
        return CommandResult.Pass();
    }
}
