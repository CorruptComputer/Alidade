using Alidade.Map.Handlers;

namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class FlyToElement(
    EditBufferStateService editBufferState,
    IMediator mediator) : IRequestHandler<FlyToElement.Command, CommandResult>
{
    /// <summary>
    ///   Pans the map viewport to the centroid of the specified element without changing zoom.
    /// </summary>
    public record Command(OsmElementRef Element) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        EditBufferState buf = editBufferState.State;

        (double lat, double lon)? pos = request.Element.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(request.Element.Id, out OsmNode? n)
                => (n.Lat, n.Lon),

            OsmElementTypes.Way when buf.Ways.TryGetValue(request.Element.Id, out OsmWay? w)
                => WayCentroid(w, buf.Nodes),

            _ => null
        };

        if (pos is null)
        {
            return CommandResult.Pass();
        }

        await mediator.Send(new PanTo.Command(pos.Value.lat, pos.Value.lon), cancellationToken);
        return CommandResult.Pass();
    }

    private static (double lat, double lon)? WayCentroid(OsmWay way, IReadOnlyDictionary<long, OsmNode> nodes)
    {
        double latSum = 0;
        double lonSum = 0;
        int count = 0;

        foreach (long id in way.NodeIds)
        {
            if (!nodes.TryGetValue(id, out OsmNode? n))
            {
                continue;
            }

            latSum += n.Lat;
            lonSum += n.Lon;
            count++;
        }

        return count == 0 ? null : (latSum / count, lonSum / count);
    }
}
