namespace Alidade.Map.Models;

/// <summary>
///   Data carried by a node drag-end event forwarded from MapLibre JS to the Blazor component tree.
/// </summary>
/// <param name="ElementId">The feature ID of the dragged node (e.g. <c>"node/12345"</c>).</param>
/// <param name="Lat">New latitude of the node after the drag, in decimal degrees.</param>
/// <param name="Lon">New longitude of the node after the drag, in decimal degrees.</param>
/// <param name="SnapTargetId">
///   Feature ID of the node under the cursor at drop time (e.g. <c>"node/67890"</c>), determined
///   by a pixel-level hit-test in JS. <c>null</c> when the drop did not land on another node.
/// </param>
/// <param name="WaySnapTargetId">
///   Feature ID of the way under the cursor at drop time (e.g. <c>"way/12345"</c>), determined
///   by a pixel-level hit-test in JS. <c>null</c> when the drop did not land on a way, or when
///   <paramref name="SnapTargetId"/> is already set (node merge takes priority).
/// </param>
/// <param name="WaySnapSegmentNodeA">
///   Node ID string of one endpoint of the way segment that is nearest to the cursor in pixel
///   space, set whenever <paramref name="WaySnapTargetId"/> is set. Allows C# to project onto
///   the JS-identified segment directly rather than re-running a coordinate-distance search.
/// </param>
/// <param name="WaySnapSegmentNodeB">
///   Node ID string of the other endpoint of the nearest snap segment.
/// </param>
public record NodeDragEvent(
    string ElementId,
    double Lat,
    double Lon,
    string? SnapTargetId = null,
    string? WaySnapTargetId = null,
    string? WaySnapSegmentNodeA = null,
    string? WaySnapSegmentNodeB = null);
