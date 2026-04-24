namespace Alidade.Osm.Models;

/// <summary>
///   A single comment in an OSM note thread.
/// </summary>
/// <param name="Action">
///   The event that produced this comment. Values from the OSM API are <c>"opened"</c>,
///   <c>"commented"</c>, <c>"closed"</c>, and <c>"reopened"</c>.
/// </param>
/// <param name="CreatedAt">UTC time this comment was posted.</param>
/// <param name="UserName">The OSM username of the commenter, or null for anonymous comments.</param>
/// <param name="Text">The comment text. May be empty for <c>"closed"</c> or <c>"reopened"</c> actions with no accompanying message.</param>
public record OsmNoteComment(string Action, DateTimeOffset CreatedAt, string? UserName, string Text);
