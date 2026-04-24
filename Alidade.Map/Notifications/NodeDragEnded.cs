namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> when the user releases a dragged node handle.
/// </summary>
public static class NodeDragEnded
{
    /// <summary>
    ///   Carries the drag result including the new position and any snap targets.
    /// </summary>
    /// <param name="Event">The drag event data forwarded from MapLibre.</param>
    public record Notification(NodeDragEvent Event) : NotificationBase;
}
