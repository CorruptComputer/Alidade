namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class ClearBackgroundImagery(MapInteropService map) : IRequestHandler<ClearBackgroundImagery.Command, CommandResult>
{
    /// <summary>
    ///   Removes the current background imagery layer.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.ClearBackgroundImageryAsync();
        return CommandResult.Pass();
    }
}
