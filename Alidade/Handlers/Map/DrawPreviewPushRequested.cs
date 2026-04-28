using Alidade.Map.Handlers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Handlers.Map;

/// <inheritdoc />
public sealed class DrawPreviewPushRequested(ToolStateService toolState, IMediator mediator, GeometryFactory geomFactory)
    : INotificationHandler<DrawPreviewPushRequested.Notification>
{
    /// <summary>
    ///   Raised when the in-progress draw tool state changes and the rubber-band preview needs updating.
    /// </summary>
    public record Notification : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        FeatureCollection fc = [];
        ImmutableList<(double Lat, double Lon, long? NodeId)> pts = toolState.State.WayInProgress;

        if (pts.Count >= 2)
        {
            Coordinate[] coords = [.. pts.Select(p => new Coordinate(p.Lon, p.Lat))];
            fc.Add(new Feature(geomFactory.CreateLineString(coords), new AttributesTable()));
        }

        ActiveTools active = toolState.State.Active;
        int terminalIndex = active == ActiveTools.DrawArea ? 0 : pts.Count - 1;

        for (int i = 0; i < pts.Count; i++)
        {
            (double lat, double lon, long? _) = pts[i];
            AttributesTable attrs = new() { { "index", i } };
            if (i == terminalIndex)
            {
                attrs.Add("terminal", true);
            }

            fc.Add(new Feature(geomFactory.CreatePoint(new Coordinate(lon, lat)), attrs));
        }

        await mediator.Send(new SetSourceData.Command(MapSourceNames.DrawPreview, fc));
    }
}
