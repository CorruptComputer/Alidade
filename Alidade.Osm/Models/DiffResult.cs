namespace Alidade.Osm.Models;

/// <summary>
///   Maps old temporary negative IDs to the permanent IDs assigned by the OSM API after upload.
///   Only elements that were newly created (and thus had negative placeholder IDs) appear as keys;
///   modified and deleted elements are not included.
/// </summary>
/// <param name="NodeIdMap">Maps each created node's temporary negative ID to its assigned permanent ID.</param>
/// <param name="WayIdMap">Maps each created way's temporary negative ID to its assigned permanent ID.</param>
/// <param name="RelationIdMap">Maps each created relation's temporary negative ID to its assigned permanent ID.</param>
public record DiffResult(
    IReadOnlyDictionary<long, long> NodeIdMap,
    IReadOnlyDictionary<long, long> WayIdMap,
    IReadOnlyDictionary<long, long> RelationIdMap);
