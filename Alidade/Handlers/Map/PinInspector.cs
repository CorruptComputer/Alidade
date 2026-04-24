namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class PinInspector(MapStateService mapState) : IRequestHandler<PinInspector.Command, CommandResult>
{
    /// <summary>
    ///   Adds an element to the pinned inspectors list (no-op if already pinned).
    /// </summary>
    public record Command(OsmElementRef Element) : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        if (!mapState.State.PinnedInspectors.Contains(request.Element))
        {
            mapState.SetState(mapState.State with
            {
                PinnedInspectors = mapState.State.PinnedInspectors.Add(request.Element)
            });
        }
        return Task.FromResult(CommandResult.Pass());
    }
}
