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
    ImmutableList<OsmElementRef> PinnedInspectors)
{
    /// <summary>
    ///   Initializes with no bounds, all panels hidden, and no pinned inspectors.
    /// </summary>
    public MapState() : this(null, false, false, false, false, []) { }
}
