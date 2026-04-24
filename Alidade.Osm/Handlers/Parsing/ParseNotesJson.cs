using System.Text.Json;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Models;
using Questy;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class ParseNotesJson : IRequestHandler<ParseNotesJson.Query, QueryResult<IList<OsmNote>>>
{
    /// <summary>
    ///   Parses the given GeoJSON string into a list of notes.
    /// </summary>
    /// <param name="Json">The raw GeoJSON FeatureCollection string from the notes endpoint.</param>
    public record Query(string Json) : IRequest<QueryResult<IList<OsmNote>>>;

    /// <inheritdoc />
    public Task<QueryResult<IList<OsmNote>>> Handle(Query request, CancellationToken cancellationToken)
    {
        using JsonDocument doc = JsonDocument.Parse(request.Json);
        JsonElement features = doc.RootElement.GetProperty("features");
        List<OsmNote> notes = [];

        foreach (JsonElement f in features.EnumerateArray())
        {
            JsonElement props = f.GetProperty("properties");
            JsonElement coords = f.GetProperty("geometry").GetProperty("coordinates");

            notes.Add(new OsmNote(
                props.GetProperty("id").GetInt64(),
                coords[1].GetDouble(),
                coords[0].GetDouble(),
                props.GetProperty("status").GetString() ?? "open",
                DateTimeOffset.Parse(props.GetProperty("date_created").GetString()!),
                [.. props.GetProperty("comments").GetProperty("comments").EnumerateArray()
                    .Select(c => new OsmNoteComment(c.GetProperty("action").GetString() ?? string.Empty,
                                                    DateTimeOffset.Parse(c.GetProperty("date").GetString()!),
                                                    c.TryGetProperty("user", out JsonElement u)
                                                        ? u.GetString()
                                                        : null,
                                                    c.GetProperty("text").GetString() ?? string.Empty))]));
        }

        return Task.FromResult<QueryResult<IList<OsmNote>>>(notes);
    }
}
