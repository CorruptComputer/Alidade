namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CloseChangeset(IOsmEditingService osm) : IRequestHandler<CloseChangeset.Command, CommandResult>
{
    /// <summary>
    ///   Closes the changeset with the given ID.
    /// </summary>
    /// <param name="ChangesetId">The changeset ID to close.</param>
    public record Command(int ChangesetId) : IRequest<CommandResult>;

    /// <inheritdoc />
    public async Task<CommandResult> Handle(Command request, CancellationToken cancellationToken)
    {
        await osm.CloseChangesetAsync(request.ChangesetId, cancellationToken);
        return CommandResult.Pass();
    }
}
