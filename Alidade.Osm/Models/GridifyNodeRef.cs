namespace Alidade.Osm.Models;

/// <summary>
///   A reference to a node within a <see cref="GridifyResult"/>: either a newly created node
///   (referenced by its index into <see cref="GridifyResult.NewNodes"/>) or an existing OSM node
///   that is being reused from the original way boundary (referenced by its ID).
/// </summary>
public readonly struct GridifyNodeRef
{
    /// <summary>
    ///   Whether this reference points to an existing OSM node rather than a new one.
    /// </summary>
    public bool IsExisting { get; }

    /// <summary>
    ///   Index into <see cref="GridifyResult.NewNodes"/>. Valid only when <see cref="IsExisting"/>
    ///   is <see langword="false"/>.
    /// </summary>
    public int NewNodeIndex { get; }

    /// <summary>
    ///   ID of the existing OSM node being reused. Valid only when <see cref="IsExisting"/>
    ///   is <see langword="true"/>.
    /// </summary>
    public long ExistingNodeId { get; }

    private GridifyNodeRef(bool isExisting, int newNodeIndex, long existingNodeId)
    {
        IsExisting = isExisting;
        NewNodeIndex = newNodeIndex;
        ExistingNodeId = existingNodeId;
    }

    /// <summary>
    ///   Creates a reference to a new node at position <paramref name="index"/> in
    ///   <see cref="GridifyResult.NewNodes"/>.
    /// </summary>
    /// <param name="index">Zero-based index into the new-node list.</param>
    /// <returns>A new-node reference.</returns>
    public static GridifyNodeRef New(int index) => new(false, index, 0);

    /// <summary>
    ///   Creates a reference to an existing OSM node with the given <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The OSM node ID to reuse.</param>
    /// <returns>An existing-node reference.</returns>
    public static GridifyNodeRef Existing(long id) => new(true, 0, id);
}
