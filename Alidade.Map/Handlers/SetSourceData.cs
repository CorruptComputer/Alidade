using System.Text.Json;
using NetTopologySuite.Features;

namespace Alidade.Map.Handlers;

/// <inheritdoc />
public sealed class SetSourceData(MapInteropService map, JsonSerializerOptions geoJsonOptions)
    : IRequestHandler<SetSourceData.Command, CommandResult>
{
    /// <summary>
    ///   Serializes <paramref name="Features"/> to GeoJSON and replaces the data for a named
    ///   MapLibre source. Serialization is performed inside the handler so it is captured by
    ///   pipeline timing.
    /// </summary>
    /// <param name="SourceId">The MapLibre source ID (e.g. <c>"osm-nodes"</c>).</param>
    /// <param name="Features">The feature collection to publish as GeoJSON.</param>
    public record Command(string SourceId, FeatureCollection Features) : IRequest<CommandResult>
    {
        /// <inheritdoc />
        public override string ToString() => $"SetSourceData: {SourceId} ({Features.Count} features)";
    }

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(request.Features, geoJsonOptions);
        await map.SetSourceDataAsync(request.SourceId, json);
        return CommandResult.Pass();
    }
}
