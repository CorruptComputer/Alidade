namespace Alidade.Osm.Models.Tools.Circularize;

/// <summary>
///   A reference to a node in a circularized way's final node list, distinguishing between
///   existing nodes (identified by ID) and newly-created nodes (identified by their insertion
///   index into the new-node subset of <see cref="CircularizeResult.Moves"/>).
/// </summary>
public readonly struct CircularizeNodeRef
{
    /// <summary>
    ///   Gets a value indicating whether this reference points to a newly-created node.
    /// </summary>
    public bool IsNew { get; private init; }

    /// <summary>
    ///   Gets the ID of the existing node.
    ///   Only valid when <see cref="IsNew"/> is <see langword="false"/>.
    /// </summary>
    public long ExistingId { get; private init; }

    /// <summary>
    ///   Gets the index into the new-node subset of <see cref="CircularizeResult.Moves"/>
    ///   (entries where <c>Old</c> is <see langword="null"/>).
    ///   Only valid when <see cref="IsNew"/> is <see langword="true"/>.
    /// </summary>
    public int NewIndex { get; private init; }

    /// <summary>
    ///   Creates a reference to an existing node by ID.
    /// </summary>
    /// <param name="id">The ID of the existing node.</param>
    /// <returns>A <see cref="CircularizeNodeRef"/> pointing to the existing node.</returns>
    public static CircularizeNodeRef Existing(long id)
        => new() { IsNew = false, ExistingId = id };

    /// <summary>
    ///   Creates a reference to a new node by its insertion index.
    /// </summary>
    /// <param name="index">
    ///   The index into the new-node subset of <see cref="CircularizeResult.Moves"/>
    ///   (entries where <c>Old</c> is <see langword="null"/>).
    /// </param>
    /// <returns>A <see cref="CircularizeNodeRef"/> pointing to the new node.</returns>
    public static CircularizeNodeRef New(int index)
        => new() { IsNew = true, NewIndex = index };
}
