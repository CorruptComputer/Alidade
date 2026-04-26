using System.Diagnostics.CodeAnalysis;

namespace Alidade.Osm.Models.EditBuffer;

/// <summary>
///   Holds all OSM elements currently loaded into the editor, their local edit states,
///   and a counter for assigning temporary negative IDs to newly created elements.
/// </summary>
public record EditBufferState
{
    /// <summary>
    ///   Initializes an empty edit buffer with no elements.
    /// </summary>
    [SetsRequiredMembers]
    public EditBufferState() : this([], [], [], [], -1L) { }

    /// <summary>
    ///   Initializes a new edit buffer state with the provided elements, edit states, and next negative ID.
    /// </summary>
    /// <param name="Nodes"></param>
    /// <param name="Ways"></param>
    /// <param name="Relations"></param>
    /// <param name="EditStates"></param>
    /// <param name="NextNegativeId"></param>
    [SetsRequiredMembers]
    public EditBufferState(ImmutableDictionary<long, OsmNode> Nodes, ImmutableDictionary<long, OsmWay> Ways,
                           ImmutableDictionary<long, OsmRelation> Relations, ImmutableDictionary<OsmElementRef, EditState> EditStates,
                           long NextNegativeId)
    {
        this.Nodes = Nodes;
        this.Ways = Ways;
        this.Relations = Relations;
        this.EditStates = EditStates;
        this.NextNegativeId = NextNegativeId;
    }

    /// <summary>
    ///   The OSM nodes currently loaded in the edit buffer, indexed by their IDs.
    /// </summary>
    public required ImmutableDictionary<long, OsmNode> Nodes { get; init; }

    /// <summary>
    ///   The OSM ways currently loaded in the edit buffer, indexed by their IDs.
    /// </summary>
    public required ImmutableDictionary<long, OsmWay> Ways { get; init; }

    /// <summary>
    ///   The OSM relations currently loaded in the edit buffer, indexed by their IDs.
    /// </summary>
    public required ImmutableDictionary<long, OsmRelation> Relations { get; init; }

    /// <summary>
    ///   The edit states of all elements currently in the edit buffer, indexed by their element references.
    /// </summary>
    public required ImmutableDictionary<OsmElementRef, EditState> EditStates { get; init; }

    /// <summary>
    ///   The next negative ID to assign to a newly created element.
    /// </summary>
    public required long NextNegativeId { get; init; }

    /// <summary>
    ///   Imagery sources used during this edit session, appended as the user switches layers.
    ///   Persisted in the draft so that the tag survives a page refresh.
    /// </summary>
    public ImmutableList<string> ImageryUsed { get; init; } = [];

    /// <summary>
    ///   Returns <see langword="true"/> when any element has been created, modified, or deleted.
    /// </summary>
    public bool IsDirty
        => EditStates.Values.Any(s => s != EditState.Fetched);
}
