namespace Alidade.Core.PipelineBehaviors;

/// <inheritdoc />
public sealed class CommandBehavior<TRequest>(ILogger<CommandBehavior<TRequest>> logger)
    : PipelineBehaviorBase<TRequest, CommandResult>(logger) where TRequest : notnull
{
    /// <inheritdoc />
    protected override bool GetResult(CommandResult response) => response.Success;

    /// <inheritdoc />
    protected override CommandResult GetGenericFailedResponse() => CommandResult.Fail();
}
