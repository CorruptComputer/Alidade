namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class UnpinInspector(MapStateService mapState) : IRequestHandler<UnpinInspector.Command, CommandResult>
{
    /// <summary>
    ///   Removes an element from the pinned inspectors list.
    /// </summary>
    public record Command(OsmElementRef Element) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with
        {
            PinnedInspectors = mapState.State.PinnedInspectors.Remove(request.Element)
        });
        return Task.FromResult(CommandResult.Pass());
    }
}
