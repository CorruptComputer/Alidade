using System.Diagnostics;

namespace Alidade.Core.PipelineBehaviors;

/// <summary>
///   Base pipeline behavior for Questy.
/// </summary>
public abstract class PipelineBehaviorBase<TRequest, TResponse>(ILogger<PipelineBehaviorBase<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    ///   Gets the basic result info from the response.
    /// </summary>
    protected abstract bool GetResult(TResponse response);

    /// <summary>
    ///   Generates a failure response for the given type.
    /// </summary>
    protected abstract TResponse GetGenericFailedResponse(string? failReason = null);

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        Stopwatch? stopwatch = null;
        bool debugEnabled = logger.IsEnabled(LogLevel.Debug);

        if (debugEnabled)
        {
            logger.LogDebug("Started [{TypeName}] TRequest = {RequestBody}",
                typeof(TRequest).FullName, request.ToString());
            stopwatch = Stopwatch.StartNew();
        }

        Exception? exception = null;
        TResponse response;

        try
        {
            response = await next(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Request cancelled [{TypeName}]", typeof(TRequest).FullName);
            return GetGenericFailedResponse("Operation cancelled.");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Uncaught Exception [{RequestName}] | ExceptionMessage = {Message}",
                typeof(TRequest).FullName, e.Message);
            exception = e;
            response = GetGenericFailedResponse($"Uncaught exception from: {typeof(TRequest).FullName}");
        }

        bool success = GetResult(response);

        if (debugEnabled && stopwatch is not null)
        {
            stopwatch.Stop();
            if (exception is not null)
            {
                logger.LogDebug("Uncaught Exception [{TypeName}] | Elapsed = {ElapsedMilliseconds}ms | Exception = {ExceptionMessage} | TRequest = {RequestBody}",
                    typeof(TRequest).FullName, stopwatch.ElapsedMilliseconds, exception.Message, request.ToString());
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
            else if (success)
            {
                logger.LogDebug("Succeeded [{TypeName}] | Elapsed = {ElapsedMilliseconds}ms | TRequest = {RequestBody}",
                    typeof(TRequest).FullName, stopwatch.ElapsedMilliseconds, request.ToString());
            }
            else
            {
                logger.LogDebug("Failed [{TypeName}] | Elapsed = {ElapsedMilliseconds}ms | TRequest = {RequestBody}",
                    typeof(TRequest).FullName, stopwatch.ElapsedMilliseconds, request.ToString());
            }
        }

        return response;
    }
}
