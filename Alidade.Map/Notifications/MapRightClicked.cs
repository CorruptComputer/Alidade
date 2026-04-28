namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> when the user right-clicks the map canvas.
/// </summary>
public static class MapRightClicked
{
    /// <summary>
    ///   Carries the screen position of the right-click and all OSM element IDs under the cursor,
    ///   ordered top-to-bottom by paint order.
    /// </summary>
    /// <param name="ScreenX">The X position of the right-click in CSS pixels.</param>
    /// <param name="ScreenY">The Y position of the right-click in CSS pixels.</param>
    /// <param name="ElementIds">
    ///   Feature ID strings (e.g. <c>"way/123"</c>) for all rendered OSM features under the cursor,
    ///   ordered by paint layer (top first). Empty when no feature is under the cursor.
    /// </param>
    public record Notification(double ScreenX, double ScreenY, string[] ElementIds) : NotificationBase;
}
