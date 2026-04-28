using System.Globalization;
using System.Text.Json;

namespace Alidade.Osm.Handlers.Parsing;

/// <inheritdoc />
public class ParseNotesJson : IRequestHandler<ParseNotesJson.Query, QueryResult<IList<OsmNote>>>
{
    /// <summary>
    ///   Parses a GeoJSON notes response from <paramref name="Stream"/> into a list of notes.
    ///   The caller retains ownership of the stream and is responsible for disposing it.
    /// </summary>
    /// <param name="Stream">A readable stream positioned at the start of the GeoJSON response.</param>
    public record Query(Stream Stream) : IRequest<QueryResult<IList<OsmNote>>>
    {
        /// <inheritdoc />
        public override string ToString() => "ParseNotesJson: streaming";
    }

    /// <inheritdoc />
    public async Task<QueryResult<IList<OsmNote>>> Handle(Query request, CancellationToken cancellationToken)
    {
        using JsonDocument doc = await JsonDocument.ParseAsync(request.Stream, cancellationToken: cancellationToken);
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
                DateTimeOffset.ParseExact(props.GetProperty("date_created").GetString()!, "yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
                [.. props.GetProperty("comments").GetProperty("comments").EnumerateArray()
                    .Select(c => new OsmNoteComment(c.GetProperty("action").GetString() ?? string.Empty,
                                                    DateTimeOffset.ParseExact(c.GetProperty("date").GetString()!, "yyyy-MM-dd HH:mm:ss UTC", CultureInfo.InvariantCulture),
                                                    c.TryGetProperty("user", out JsonElement u)
                                                        ? u.GetString()
                                                        : null,
                                                    c.GetProperty("text").GetString() ?? string.Empty))]));
        }

        return notes;
    }
}
