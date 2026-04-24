namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> when the user clicks the map canvas.
/// </summary>
public static class MapClicked
{
    /// <summary>
    ///   Carries the click location and the feature under the cursor.
    /// </summary>
    /// <param name="Event">The click event data forwarded from MapLibre.</param>
    public record Notification(MapClickEvent Event) : NotificationBase;
}
