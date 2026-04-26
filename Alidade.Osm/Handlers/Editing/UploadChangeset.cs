using Alidade.Osm.Handlers.Parsing;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class UploadChangeset(IOsmEditingService osm, ISender sender) : IRequestHandler<UploadChangeset.Query, QueryResult<DiffResult>>
{
    /// <summary>
    ///   Uploads the given change to an open changeset and returns the diff result.
    /// </summary>
    /// <param name="ChangesetId">The open changeset to upload to.</param>
    /// <param name="Change">The set of created, modified, and deleted elements.</param>
    public record Query(int ChangesetId, OsmChange Change) : IRequest<QueryResult<DiffResult>>;

    /// <inheritdoc />
    public async Task<QueryResult<DiffResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        QueryResult<string> buildResult = await sender.Send(new BuildOsmChangeXml.Query(request.ChangesetId, request.Change), cancellationToken);

        if (!buildResult.Success || buildResult.Result is null)
        {
            return QueryResult<DiffResult>.Fail(buildResult.FailReason);
        }

        string diffXml = await osm.UploadChangesetAsync(request.ChangesetId, buildResult.Result, cancellationToken);

        QueryResult<DiffResult> parseResult = await sender.Send(new ParseDiffResult.Query(diffXml), cancellationToken);

        return parseResult;
    }
}
