namespace Alidade.Osm.Models;

/// <summary>
///   An OSM way (linear or area geometry) with an ordered list of node references and tags.
/// </summary>
/// <param name="Id">
///   The way's numeric ID. Negative values are temporary placeholder IDs for locally created
///   ways that have not yet been uploaded.
/// </param>
/// <param name="Version">
///   The server-assigned version number, incremented on each upload. <c>0</c> for locally
///   created ways that have never been uploaded.
/// </param>
/// <param name="ChangesetId">The ID of the changeset that last modified this way, or null for locally created ways.</param>
/// <param name="UserName">The OSM username of the last editor, or null if not available.</param>
/// <param name="Timestamp">The UTC time of the last server-side edit, or null for locally created ways.</param>
/// <param name="NodeIds">
///   Ordered list of node IDs that form the way's geometry. For closed ways the first and
///   last entries are the same node ID. May contain negative IDs for locally created nodes
///   that are part of the same unsaved edit.
/// </param>
/// <param name="Tags">Key-value pairs describing the way.</param>
public record OsmWay(
    long Id,
    int Version,
    int? ChangesetId,
    string? UserName,
    DateTimeOffset? Timestamp,
    IReadOnlyList<long> NodeIds,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>
    ///   Returns a typed reference to this way.
    /// </summary>
    public OsmElementRef Ref => new(OsmElementTypes.Way, Id);

    /// <summary>
    ///   Returns <see langword="true"/> when the first and last node IDs are identical.
    /// </summary>
    public bool IsClosed => NodeIds.Count >= 2 && NodeIds[0] == NodeIds[^1];

    // Tag keys that make a closed way implicitly an area even without area=yes.
    private static readonly HashSet<string> AreaImplyingKeys =
    [
        "building", "building:part", "landuse", "leisure", "natural",
        "amenity", "shop", "place", "man_made", "military", "aeroway",
        "boundary", "historic"
    ];

    /// <summary>
    ///   Returns <see langword="true"/> when the way is a closed ring that represents an
    ///   area polygon. A closed way is an area when:
    ///   <list type="bullet">
    ///     <item><description><c>area=no</c> is not set (explicit opt-out), and</description></item>
    ///     <item><description><c>area=yes</c> is set, OR any area-implying tag key is present
    ///       (e.g. <c>building</c>, <c>landuse</c>, <c>leisure</c>, <c>natural</c>…).</description></item>
    ///   </list>
    /// </summary>
    public bool IsArea
        => IsClosed
            && Tags.GetValueOrDefault("area") != "no"
            && (Tags.GetValueOrDefault("area") == "yes"
                || Tags.Keys.Any(k => AreaImplyingKeys.Contains(k)));
}
