namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class SetSourceData(MapInteropService map) : IRequestHandler<SetSourceData.Command, CommandResult>
{
    /// <summary>
    ///   Replaces the GeoJSON data for a named MapLibre source.
    /// </summary>
    /// <param name="SourceId">The MapLibre source ID (e.g. <c>"osm-nodes"</c>).</param>
    /// <param name="GeoJsonJson">A serialized GeoJSON FeatureCollection string.</param>
    public record Command(string SourceId, string GeoJsonJson) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.SetSourceDataAsync(request.SourceId, request.GeoJsonJson);
        return CommandResult.Pass();
    }
}
