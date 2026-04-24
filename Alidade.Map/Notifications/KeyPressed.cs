namespace Alidade.Map.Notifications;

/// <summary>
///   Published by <see cref="MapInteropService"/> when the user presses a key outside a text input.
///   The combo string is pre-normalized by JS (e.g. <c>"ctrl+z"</c>, <c>"delete"</c>).
/// </summary>
public static class KeyPressed
{
    /// <summary>
    ///   Carries the normalized key combo string.
    /// </summary>
    /// <param name="Combo">The normalized key combo (e.g. <c>"ctrl+z"</c>).</param>
    public record Notification(string Combo) : NotificationBase;
}
