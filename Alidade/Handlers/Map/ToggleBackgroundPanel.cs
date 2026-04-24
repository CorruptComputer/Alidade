namespace Alidade.Handlers.Map;

/// <inheritdoc />
public class ToggleBackgroundPanel(MapStateService mapState) : IRequestHandler<ToggleBackgroundPanel.Command, CommandResult>
{
    /// <summary>
    ///   Toggles the background imagery selector panel.
    /// </summary>
    public record Command : IRequest<CommandResult>;

    /// <inheritdoc />
    public Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        mapState.SetState(mapState.State with { BackgroundPanelVisible = !mapState.State.BackgroundPanelVisible });
        return Task.FromResult(CommandResult.Pass());
    }
}
