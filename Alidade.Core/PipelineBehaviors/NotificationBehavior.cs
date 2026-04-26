using System.Diagnostics;

namespace Alidade.Core.PipelineBehaviors;

/// <summary>
///  Notification pipeline behavior for Questy.
/// </summary>
/// <typeparam name="TNotification">The notification type being handled.</typeparam>
public sealed class NotificationBehavior<TNotification>(INotificationHandler<TNotification> inner, ILogger<NotificationBehavior<TNotification>> logger, Lazy<IMediator> mediator)
    : INotificationHandler<TNotification> where TNotification : INotification
{
    /// <inheritdoc />
    public async Task Handle(TNotification notification, CancellationToken cancellationToken)
    {
        Stopwatch? stopwatch = null;
        bool debugEnabled = logger.IsEnabled(LogLevel.Debug);

        if (debugEnabled)
        {
            logger.LogDebug("Started [{TypeName}] TNotification = {NotificationBody}",
                typeof(TNotification).FullName, notification.ToString());
            stopwatch = Stopwatch.StartNew();
        }

        Exception? exception = null;

        try
        {
            await inner.Handle(notification, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Request cancelled [{TypeName}]", typeof(TNotification).FullName);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Uncaught Exception [{NotificationName}] | ExceptionMessage = {Message}",
                typeof(TNotification).FullName, e.Message);
            exception = e;
        }

        if (exception is null && notification is NotificationBase nb)
        {
            foreach (NotificationBase followUp in nb.ContinueWith)
            {
                await mediator.Value.Publish(followUp, cancellationToken);
            }
        }

        if (debugEnabled && stopwatch is not null)
        {
            stopwatch.Stop();

            if (exception is not null)
            {
                logger.LogDebug("Uncaught Exception [{TypeName}] | Exception = {ExceptionMessage} | TNotification = {NotificationBody} | Elapsed = {ElapsedMilliseconds}ms",
                    typeof(TNotification).FullName, exception.Message, notification.ToString(), stopwatch.ElapsedMilliseconds);

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
            else
            {
                logger.LogDebug("Succeeded [{TypeName}] | TNotification = {NotificationBody} | Elapsed = {ElapsedMilliseconds}ms",
                    typeof(TNotification).FullName, notification.ToString(), stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
