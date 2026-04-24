using Alidade.Core.Models.CQRS.Response;
using Questy;

namespace Alidade.Osm.Handlers.Editing;

/// <inheritdoc />
public class CreateChangeset(IOsmEditingService osm) : IRequestHandler<CreateChangeset.Query, QueryResult<int>>
{
    /// <summary>
    ///   Creates a changeset with the given tags.
    /// </summary>
    /// <param name="Tags">The changeset tags (e.g. <c>comment</c>, <c>created_by</c>).</param>
    public record Query(Dictionary<string, string> Tags) : IRequest<QueryResult<int>>;

    /// <inheritdoc />
    public async Task<QueryResult<int>> Handle(Query request, CancellationToken cancellationToken)
    {
        int id = await osm.CreateChangesetAsync(request.Tags, cancellationToken);

        return id;
    }
}
