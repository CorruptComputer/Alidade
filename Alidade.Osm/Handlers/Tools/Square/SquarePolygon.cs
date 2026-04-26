using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class SquarePolygon(GeometryFactory factory)
    : IRequestHandler<SquarePolygon.Query, QueryResult<IFeature>>
{
    /// <summary>
    ///   Computes a squared version of an NTS <see cref="Polygon"/> feature by applying the
    ///   iterative bisector-motion algorithm to the exterior ring. The original attributes are
    ///   preserved on the returned feature. Interior rings (holes) are kept unchanged.
    /// </summary>
    /// <param name="Feature">
    ///   The polygon feature to square. <see cref="IFeature.Geometry"/> must be a
    ///   <see cref="Polygon"/>. Coordinates are expected in WGS-84 (X = longitude, Y = latitude).
    /// </param>
    public record Query(IFeature Feature) : IRequest<QueryResult<IFeature>>;

    /// <inheritdoc />
    public Task<QueryResult<IFeature>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.Feature.Geometry is not Polygon polygon)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("Feature geometry must be a Polygon."));
        }

        IFeature? result = Apply(polygon, request.Feature.Attributes);
        if (result is null)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("The polygon cannot be squared."));
        }

        return Task.FromResult(QueryResult<IFeature>.Pass(result));
    }

    internal IFeature? Apply(Polygon polygon, IAttributesTable attributes)
    {
        Coordinate[]? squaredRing = SquareRing(polygon.ExteriorRing.Coordinates, isClosed: true);
        if (squaredRing is null)
        {
            return null;
        }

        LinearRing[] holes = [.. polygon.InteriorRings.Cast<LinearRing>()];
        Polygon squared = factory.CreatePolygon(factory.CreateLinearRing(squaredRing), holes);
        return new Feature(squared, CopyAttributes(attributes));
    }

    internal static Coordinate[]? SquareRing(Coordinate[] ring, bool isClosed)
    {
        // NTS closed rings repeat the first coordinate at the end; exclude the duplicate.
        List<Coordinate> pts = isClosed
            ? [.. ring.Take(ring.Length - 1)]
            : [.. ring];

        if (pts.Count < 3)
        {
            return null;
        }

        double latRef = pts.Average(c => c.Y);

        List<double[]> flat = [.. pts.Select(c =>
        {
            Coordinate f = GeometryService.Project(c, latRef);
            return new double[] { f.X, f.Y };
        })];

        List<double[]> original = [.. flat.Select(p => new double[] { p[0], p[1] })];

        List<int> simplified = [];
        for (int i = 0; i < flat.Count; i++)
        {
            double dotp = 0;
            if (isClosed || (i > 0 && i < flat.Count - 1))
            {
                int prev = (i - 1 + flat.Count) % flat.Count;
                int next = (i + 1) % flat.Count;
                dotp = Math.Abs(SquareAlgorithm.NormalizedDot(flat[prev], flat[next], flat[i]));
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
                double[] a = flat[(si - 1 + flat.Count) % flat.Count];
                double[] o = flat[si];
                double[] b = flat[(si + 1) % flat.Count];
                return SquareAlgorithm.CalcMotion(o, a, b, isClosed, mi, simplified.Count);
            })];

            for (int mi = 0; mi < simplified.Count; mi++)
            {
                flat[simplified[mi]][0] += motions[mi][0];
                flat[simplified[mi]][1] += motions[mi][1];
            }

            double newScore = SquareAlgorithm.CalcScore(flat, simplified, isClosed);
            if (newScore < score)
            {
                score = newScore;
            }

            if (score < SquareAlgorithm.Epsilon)
            {
                break;
            }
        }

        bool anyMoved = false;
        Coordinate[] outRing = new Coordinate[pts.Count + (isClosed ? 1 : 0)];
        for (int i = 0; i < pts.Count; i++)
        {
            double dx = flat[i][0] - original[i][0];
            double dy = flat[i][1] - original[i][1];
            if (Math.Sqrt(dx * dx + dy * dy) >= SquareAlgorithm.Epsilon)
            {
                anyMoved = true;
            }

            outRing[i] = GeometryService.Unproject(new Coordinate(flat[i][0], flat[i][1]), latRef);
        }

        if (!anyMoved)
        {
            return null;
        }

        if (isClosed)
        {
            outRing[pts.Count] = outRing[0];
        }

        return outRing;
    }

    private static AttributesTable CopyAttributes(IAttributesTable source)
    {
        AttributesTable copy = new();
        foreach (string name in source.GetNames())
        {
            copy.Add(name, source[name]);
        }

        return copy;
    }
}
