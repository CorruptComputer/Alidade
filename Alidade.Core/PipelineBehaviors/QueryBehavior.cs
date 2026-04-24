namespace Alidade.Core.PipelineBehaviors;

/// <inheritdoc />
public sealed class QueryBehavior<TRequest, TValue>(ILogger<QueryBehavior<TRequest, TValue>> logger)
    : PipelineBehaviorBase<TRequest, QueryResult<TValue>>(logger) where TRequest : notnull
{
    /// <inheritdoc />
    protected override bool GetResult(QueryResult<TValue> response) => response.Success;

    /// <inheritdoc />
    protected override QueryResult<TValue> GetGenericFailedResponse() => QueryResult<TValue>.Fail();
}
