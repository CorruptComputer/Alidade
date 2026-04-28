namespace Alidade.Models.Map;

/// <summary>
///   Holds the current viewport bounds and visibility flags for map-level UI panels.
/// </summary>
public record MapState(
    MapBounds? CurrentBounds,
    bool BackgroundPanelVisible,
    bool UploadDialogVisible,
    bool SettingsPanelVisible,
    bool GridifyDialogVisible,
    bool CircularizeDialogVisible,
    ImmutableList<OsmElementRef> PinnedInspectors,
    bool ContextMenuVisible,
    double ContextMenuX,
    double ContextMenuY,
    OsmElementRef? ContextMenuTargetElement)
{
    /// <summary>
    ///   Initializes with no bounds, all panels hidden, no pinned inspectors, and no context menu.
    /// </summary>
    public MapState() : this(null, false, false, false, false, false, [], false, 0, 0, null) { }
}
