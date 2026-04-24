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
    protected abstract TResponse GetGenericFailedResponse();

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
        catch (Exception e)
        {
            logger.LogError(e, "Uncaught Exception [{RequestName}] | ExceptionMessage = {Message}",
                typeof(TRequest).FullName, e.Message);
            exception = e;
            response = GetGenericFailedResponse();
        }

        bool success = GetResult(response);

        if (debugEnabled && stopwatch is not null)
        {
            stopwatch.Stop();
            if (exception is not null)
            {
                logger.LogDebug("Uncaught Exception [{TypeName}] | Exception = {ExceptionMessage} | TRequest = {RequestBody} | Elapsed = {ElapsedMilliseconds}ms",
                    typeof(TRequest).FullName, exception.Message, request.ToString(), stopwatch.ElapsedMilliseconds);
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
            else if (success)
            {
                logger.LogDebug("Succeeded [{TypeName}] | TRequest = {RequestBody} | Elapsed = {ElapsedMilliseconds}ms",
                    typeof(TRequest).FullName, request.ToString(), stopwatch.ElapsedMilliseconds);
            }
            else
            {
                logger.LogDebug("Failed [{TypeName}] | TRequest = {RequestBody} | Elapsed = {ElapsedMilliseconds}ms",
                    typeof(TRequest).FullName, request.ToString(), stopwatch.ElapsedMilliseconds);
            }
        }

        return response;
    }
}
