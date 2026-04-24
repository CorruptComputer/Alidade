namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> when the pointer moves over a new feature
///   or leaves all features.
/// </summary>
public static class HoverChanged
{
    /// <summary>
    ///   Carries the feature ID under the cursor, or <c>null</c> when the pointer leaves all features.
    /// </summary>
    /// <param name="ElementId">
    ///   The feature ID string (e.g. <c>"node/12345"</c>), or <c>null</c> when leaving.
    /// </param>
    public record Notification(string? ElementId) : NotificationBase;
}
