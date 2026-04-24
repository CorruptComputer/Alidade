namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class SetBackgroundImagery(MapInteropService map) : IRequestHandler<SetBackgroundImagery.Command, CommandResult>
{
    /// <summary>
    ///   Switches the background imagery to a TMS or XYZ tile provider.
    /// </summary>
    /// <param name="Tiles">One or more tile URL templates.</param>
    /// <param name="TileSize">Tile size in pixels (typically 256 or 512).</param>
    /// <param name="Attribution">Attribution text, or <c>null</c> for none.</param>
    /// <param name="TmsScheme">Whether the URL uses TMS (inverted Y) tile addressing.</param>
    /// <param name="MaxZoom">Optional maximum zoom level for the source.</param>
    public record Command(string[] Tiles, int TileSize, string? Attribution, bool TmsScheme, int? MaxZoom = null)
        : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.SetBackgroundImageryAsync(request.Tiles, request.TileSize, request.Attribution, request.TmsScheme, request.MaxZoom);
        return CommandResult.Pass();
    }
}
