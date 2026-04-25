using System.Text.Json;
using Alidade.Map.Handlers;
using Alidade.Osm.Handlers.Editing;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class NotesFetchRequested(IMediator mediator, JsonSerializerOptions geoJsonOptions, GeometryFactory geomFactory)
    : INotificationHandler<NotesFetchRequested.Notification>
{

    /// <summary>
    ///   Raised when the viewport enters a new area and OSM notes should be fetched and displayed.
    /// </summary>
    /// <param name="Bounds">The map bounds to fetch notes for.</param>
    public record Notification(MapBounds Bounds) : NotificationBase;

    /// <inheritdoc />
    public async Task Handle(Notification notification, CancellationToken cancellationToken)
    {
        OsmNote[]? notes = await mediator.Send(
            new FetchNotes.Query(
                notification.Bounds.West, notification.Bounds.South,
                notification.Bounds.East, notification.Bounds.North),
            cancellationToken);

        if (notes is null)
        {
            return;
        }

        FeatureCollection fc = new();
        foreach (OsmNote note in notes)
        {
            AttributesTable attrs = new();
            attrs.Add("id", note.Id);
            attrs.Add("status", note.Status);
            OsmNoteComment? firstComment = note.Comments.FirstOrDefault();
            if (firstComment is not null)
            {
                attrs.Add("text", firstComment.Text);
            }
            fc.Add(new Feature(geomFactory.CreatePoint(new Coordinate(note.Lon, note.Lat)), attrs));
        }

        string json = JsonSerializer.Serialize(fc, geoJsonOptions);
        await mediator.Send(new SetSourceData.Command("osm-notes", json));
    }
}
