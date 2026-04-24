namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> after a pan or zoom gesture completes.
/// </summary>
public static class MapMoveEnded
{
    /// <summary>
    ///   Carries the new viewport bounds after the map finishes moving.
    /// </summary>
    /// <param name="Bounds">The updated geographic bounds and zoom level.</param>
    public record Notification(MapBounds Bounds) : NotificationBase;
}
