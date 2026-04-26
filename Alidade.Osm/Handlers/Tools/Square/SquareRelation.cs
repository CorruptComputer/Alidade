using Alidade.Osm.Models.Tools.Square;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class SquareRelation(EditBufferStateService editBufferState)
    : IRequestHandler<SquareRelation.Query, QueryResult<SquareResult>>
{
    /// <summary>
    ///   Computes node moves that square all way members of a relation collectively, so every
    ///   member way converges to the same set of orthogonal axes rather than each way finding
    ///   its own independent axes. Uses a single unified iteration loop with a global score
    ///   threshold shared across all rings; the common projection reference latitude is derived
    ///   from the centroid of all member way nodes.
    /// </summary>
    /// <param name="RelationId">The ID of the relation whose way members should be squared.</param>
    public record Query(long RelationId) : IRequest<QueryResult<SquareResult>>;

    /// <inheritdoc />
    public Task<QueryResult<SquareResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        EditBufferState buf = editBufferState.State;

        if (!buf.Relations.TryGetValue(request.RelationId, out OsmRelation? relation))
        {
            return Task.FromResult(QueryResult<SquareResult>.Fail("Relation not found."));
        }

        // Collect node lists for each way member (outer and inner rings).
        List<(List<OsmNode> Nodes, bool IsClosed)> rings = [];
        foreach (OsmMember member in relation.Members)
        {
            if (member.Type != OsmElementTypes.Way)
            {
                continue;
            }

            if (!buf.Ways.TryGetValue(member.Ref, out OsmWay? way))
            {
                continue;
            }

            List<long> nodeIds = way.IsClosed
                ? [.. way.NodeIds.Take(way.NodeIds.Count - 1)]
                : [.. way.NodeIds];

            List<OsmNode> nodeList = [.. nodeIds.Where(buf.Nodes.ContainsKey).Select(id => buf.Nodes[id])];
            if (nodeList.Count >= 3)
            {
                rings.Add((nodeList, way.IsClosed));
            }
        }

        if (rings.Count == 0)
        {
            return Task.FromResult(QueryResult<SquareResult>.Fail("No squarable way members found."));
        }

        // Shared projection reference keeps all rings in the same flat coordinate frame.
        double latRef = rings.SelectMany(r => r.Nodes).Average(n => n.Lat);

        // Project all rings to flat space and classify corner nodes.
        List<(List<double[]> Pts, List<double[]> Original, List<int> Simplified, bool IsClosed, List<OsmNode> Nodes)> projected = [];
        foreach ((List<OsmNode> nodeList, bool isClosed) in rings)
        {
            List<double[]> pts = [.. nodeList.Select(n =>
            {
                Coordinate flat = GeometryService.Project(new Coordinate(n.Lon, n.Lat), latRef);
                return new double[] { flat.X, flat.Y };
            })];

            List<double[]> original = [.. pts.Select(p => new double[] { p[0], p[1] })];

            List<int> simplified = [];
            for (int i = 0; i < pts.Count; i++)
            {
                double dotp = 0;
                if (isClosed || (i > 0 && i < pts.Count - 1))
                {
                    int prev = (i - 1 + pts.Count) % pts.Count;
                    int next = (i + 1) % pts.Count;
                    dotp = Math.Abs(SquareAlgorithm.NormalizedDot(pts[prev], pts[next], pts[i]));
                }

                if (dotp <= SquareAlgorithm.UpperThreshold)
                {
                    simplified.Add(i);
                }
            }

            projected.Add((pts, original, simplified, isClosed, nodeList));
        }

        // Unified iteration loop: all rings are updated each step and share the global score.
        // This ensures convergence is not declared until every member way is squared.
        double score = double.MaxValue;
        for (int iter = 0; iter < SquareAlgorithm.MaxIterations; iter++)
        {
            foreach ((List<double[]> pts, _, List<int> simplified, bool isClosed, _) in projected)
            {
                List<double[]> motions = [.. simplified.Select((si, mi) =>
                {
                    double[] a = pts[(si - 1 + pts.Count) % pts.Count];
                    double[] o = pts[si];
                    double[] b = pts[(si + 1) % pts.Count];
                    return SquareAlgorithm.CalcMotion(o, a, b, isClosed, mi, simplified.Count);
                })];

                for (int mi = 0; mi < simplified.Count; mi++)
                {
                    pts[simplified[mi]][0] += motions[mi][0];
                    pts[simplified[mi]][1] += motions[mi][1];
                }
            }

            double newScore = projected.Sum(r => SquareAlgorithm.CalcScore(r.Pts, r.Simplified, r.IsClosed));
            if (newScore < score)
            {
                score = newScore;
            }

            if (score < SquareAlgorithm.Epsilon)
            {
                break;
            }
        }

        // Aggregate moves; first occurrence wins for any node shared between member ways.
        ImmutableDictionary<long, Coordinate>.Builder moves = ImmutableDictionary.CreateBuilder<long, Coordinate>();
        foreach ((List<double[]> pts, List<double[]> original, _, _, List<OsmNode> nodeList) in projected)
        {
            for (int i = 0; i < nodeList.Count; i++)
            {
                OsmNode node = nodeList[i];
                if (moves.ContainsKey(node.Id))
                {
                    continue;
                }

                double dx = pts[i][0] - original[i][0];
                double dy = pts[i][1] - original[i][1];
                if (Math.Sqrt(dx * dx + dy * dy) < SquareAlgorithm.Epsilon)
                {
                    continue;
                }

                moves[node.Id] = GeometryService.Unproject(new Coordinate(pts[i][0], pts[i][1]), latRef);
            }
        }

        SquareResult result = new(moves.ToImmutable());
        return result.Moves.Count == 0
            ? Task.FromResult(QueryResult<SquareResult>.Fail("No nodes need to move."))
            : Task.FromResult(QueryResult<SquareResult>.Pass(result));
    }
}
