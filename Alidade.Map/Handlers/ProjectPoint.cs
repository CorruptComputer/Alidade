namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class ProjectPoint(MapInteropService map) : IRequestHandler<ProjectPoint.Query, QueryResult<ScreenPoint>>
{
    /// <summary>
    ///   Projects geographic coordinates to a CSS pixel point on the map canvas.
    /// </summary>
    /// <param name="Lat">Latitude in decimal degrees.</param>
    /// <param name="Lon">Longitude in decimal degrees.</param>
    public record Query(double Lat, double Lon) : IRequest<QueryResult<ScreenPoint>>;

    /// <inheritdoc />
    public async Task<QueryResult<ScreenPoint>> Handle(Query request, CancellationToken cancellationToken)
        => await map.ProjectAsync(request.Lat, request.Lon);
}
