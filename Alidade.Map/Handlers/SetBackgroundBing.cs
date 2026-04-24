namespace Alidade.Map.Handlers;

/// <inheritdoc />
public class SetBackgroundBing(MapInteropService map) : IRequestHandler<SetBackgroundBing.Command, CommandResult>
{
    /// <summary>
    ///   Switches the background imagery to Bing Maps aerial.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await map.SetBackgroundBingAsync();
        return CommandResult.Pass();
    }
}
