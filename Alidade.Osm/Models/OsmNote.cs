namespace Alidade.Osm.Models;

/// <summary>
///   An OSM note (map comment) with its thread of comments.
/// </summary>
/// <param name="Id">The note's unique numeric ID assigned by the OSM API.</param>
/// <param name="Lat">Latitude of the note's pin in decimal degrees (WGS 84).</param>
/// <param name="Lon">Longitude of the note's pin in decimal degrees (WGS 84).</param>
/// <param name="Status">
///   The note's current state: <c>"open"</c> for unresolved notes or <c>"closed"</c> for
///   notes that have been resolved.
/// </param>
/// <param name="CreatedAt">UTC time the note was first created.</param>
/// <param name="Comments">Ordered thread of comments, oldest first. Always contains at least the opening comment.</param>
public record OsmNote(
    long Id,
    double Lat,
    double Lon,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OsmNoteComment> Comments);
