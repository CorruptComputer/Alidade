using Alidade.Osm.Models.Tools.Square;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class SquareWay(EditBufferStateService editBufferState)
    : IRequestHandler<SquareWay.Query, QueryResult<SquareResult>>
{
    /// <summary>
    ///   Computes node moves that square the corners of a way toward the nearest 90° increment.
    ///   Implements the iterative bisector-motion algorithm from iD's <c>actionOrthogonalize</c>.
    ///   Returns a <see cref="SquareResult"/> containing the moves to commit.
    /// </summary>
    /// <param name="WayId">The ID of the way to square.</param>
    public record Query(long WayId) : IRequest<QueryResult<SquareResult>>;

    /// <inheritdoc />
    public Task<QueryResult<SquareResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        EditBufferState buf = editBufferState.State;
        SquareResult result = Compute(request.WayId, buf.Ways, buf.Nodes);
        return result.Moves.Count == 0
            ? Task.FromResult(QueryResult<SquareResult>.Fail("The way cannot be squared."))
            : Task.FromResult(QueryResult<SquareResult>.Pass(result));
    }

    private static SquareResult Compute(long wayId, ImmutableDictionary<long, OsmWay> ways, ImmutableDictionary<long, OsmNode> nodes)
    {
        if (!ways.TryGetValue(wayId, out OsmWay? way))
        {
            return SquareResult.Empty;
        }

        List<long> nodeIds = way.IsClosed
            ? [.. way.NodeIds.Take(way.NodeIds.Count - 1)]
            : [.. way.NodeIds];

        if (nodeIds.Count < 3)
        {
            return SquareResult.Empty;
        }

        List<OsmNode> nodeList = [.. nodeIds.Where(nodes.ContainsKey).Select(id => nodes[id])];
        if (nodeList.Count < 3)
        {
            return SquareResult.Empty;
        }

        double latRef = nodeList.Average(n => n.Lat);

        List<double[]> pts = [.. nodeList.Select(n =>
        {
            Coordinate flat = GeometryService.Project(new Coordinate(n.Lon, n.Lat), latRef);
            return new double[] { flat.X, flat.Y };
        })];

        List<double[]> original = [.. pts.Select(p => new double[] { p[0], p[1] })];
        bool isClosed = way.IsClosed;

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

        double score = double.MaxValue;
        for (int iter = 0; iter < SquareAlgorithm.MaxIterations; iter++)
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

            double newScore = SquareAlgorithm.CalcScore(pts, simplified, isClosed);
            if (newScore < score)
            {
                score = newScore;
            }

            if (score < SquareAlgorithm.Epsilon)
            {
                break;
            }
        }

        ImmutableDictionary<long, Coordinate>.Builder moves = ImmutableDictionary.CreateBuilder<long, Coordinate>();
        for (int i = 0; i < nodeList.Count; i++)
        {
            OsmNode node = nodeList[i];
            double dx = pts[i][0] - original[i][0];
            double dy = pts[i][1] - original[i][1];
            if (Math.Sqrt(dx * dx + dy * dy) < SquareAlgorithm.Epsilon)
            {
                continue;
            }

            moves[node.Id] = GeometryService.Unproject(new Coordinate(pts[i][0], pts[i][1]), latRef);
        }

        return new SquareResult(moves.ToImmutable());
    }
}
