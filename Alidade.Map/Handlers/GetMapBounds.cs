namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class GetMapBounds(MapInteropService map) : IRequestHandler<GetMapBounds.Query, QueryResult<MapBounds>>
{
    /// <summary>
    ///   Returns the current map viewport bounds.
    /// </summary>
    public record Query : IRequest<QueryResult<MapBounds>>;

    /// <inheritdoc />
    public async Task<QueryResult<MapBounds>> Handle(Query request, CancellationToken cancellationToken)
    {
        return await map.GetBoundsAsync();
    }
}
