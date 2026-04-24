namespace Alidade.Core.Models.CQRS.Request;

/// <summary>
///   Base class for any CQRS notification.
/// </summary>
public abstract record NotificationBase : INotification
{
    /// <summary>
    ///   If the notification completes successfully, this can be used to trigger other notifications that should be executed after this one.
    /// </summary>
    public List<NotificationBase> ContinueWith { get; init; } = [];
}
