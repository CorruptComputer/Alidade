namespace Alidade.Models.Tool;

/// <summary>
///   Holds the currently active drawing/editing tool, the nodes placed so far for an
///   in-progress way, and the ID of the nearest snap-target node.
/// </summary>
public record ToolState(ActiveTools Active, ImmutableList<(double Lat, double Lon, long? NodeId)> WayInProgress, long? SnapTargetNodeId)
{
    /// <summary>
    ///   Initializes with the Select tool active and no in-progress way.
    /// </summary>
    public ToolState() : this(ActiveTools.Select, [], null) { }
}
