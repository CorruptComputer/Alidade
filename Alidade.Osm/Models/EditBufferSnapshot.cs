namespace Alidade.Osm.Models;

/// <summary>
///   A lightweight, immutable snapshot of the edit buffer taken synchronously on the
///   UI thread before being posted to the background validation channel. The use of
///   immutable dictionaries makes it safe to read from a background thread without locking.
/// </summary>
/// <param name="Nodes">All nodes present in the buffer at snapshot time.</param>
/// <param name="Ways">All ways present in the buffer at snapshot time.</param>
/// <param name="Relations">All relations present in the buffer at snapshot time.</param>
/// <param name="EditStates">The edit state of every tracked element in the buffer.</param>
public record EditBufferSnapshot(
    ImmutableDictionary<long, OsmNode> Nodes,
    ImmutableDictionary<long, OsmWay> Ways,
    ImmutableDictionary<long, OsmRelation> Relations,
    ImmutableDictionary<OsmElementRef, EditState> EditStates);
