using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Square;

/// <inheritdoc />
public sealed class SquareMultiPolygon(GeometryFactory factory)
    : IRequestHandler<SquareMultiPolygon.Query, QueryResult<IFeature>>
{
    /// <summary>
    ///   Computes a squared version of an NTS <see cref="MultiPolygon"/> feature by applying the
    ///   iterative bisector-motion algorithm to all component exterior rings collectively. All rings
    ///   share a common projection reference latitude and a unified convergence loop, so every
    ///   component converges to the same set of orthogonal axes. Interior rings (holes) are kept
    ///   unchanged. The original attributes are preserved on the returned feature.
    /// </summary>
    /// <param name="Feature">
    ///   The multipolygon feature to square. <see cref="IFeature.Geometry"/> must be a
    ///   <see cref="MultiPolygon"/>. Coordinates are expected in WGS-84 (X = longitude, Y = latitude).
    /// </param>
    public record Query(IFeature Feature) : IRequest<QueryResult<IFeature>>;

    /// <inheritdoc />
    public Task<QueryResult<IFeature>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.Feature.Geometry is not MultiPolygon mp)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("Feature geometry must be a MultiPolygon."));
        }

        List<Polygon> polygons = [.. mp.Geometries.OfType<Polygon>()];
        if (polygons.Count == 0)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("No polygon components found."));
        }

        // Collect exterior ring coordinate lists (excluding the NTS closing duplicate).
        List<List<Coordinate>> coordRings = [.. polygons.Select(p =>
        {
            Coordinate[] ring = p.ExteriorRing.Coordinates;
            return ring.Take(ring.Length - 1).ToList();
        }).Where(r => r.Count >= 3)];

        if (coordRings.Count == 0)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("No squarable polygon rings found."));
        }

        // Shared projection reference keeps all rings in the same flat coordinate frame.
        double latRef = coordRings.SelectMany(r => r).Average(c => c.Y);

        // Project all rings to flat space and classify corner nodes.
        List<(List<double[]> Flat, List<double[]> Original, List<int> Simplified)> projected = [];
        foreach (List<Coordinate> ring in coordRings)
        {
            List<double[]> flat = [.. ring.Select(c =>
            {
                Coordinate f = GeometryService.Project(c, latRef);
                return new double[] { f.X, f.Y };
            })];

            List<double[]> original = [.. flat.Select(p => new double[] { p[0], p[1] })];

            List<int> simplified = [];
            for (int i = 0; i < flat.Count; i++)
            {
                int prev = (i - 1 + flat.Count) % flat.Count;
                int next = (i + 1) % flat.Count;
                double dotp = Math.Abs(SquareAlgorithm.NormalizedDot(flat[prev], flat[next], flat[i]));
                if (dotp <= SquareAlgorithm.UpperThreshold)
                {
                    simplified.Add(i);
                }
            }

            projected.Add((flat, original, simplified));
        }

        // Unified iteration loop with global score across all component rings.
        double score = double.MaxValue;
        for (int iter = 0; iter < SquareAlgorithm.MaxIterations; iter++)
        {
            foreach ((List<double[]> flat, _, List<int> simplified) in projected)
            {
                List<double[]> motions = [.. simplified.Select((si, mi) =>
                {
                    double[] a = flat[(si - 1 + flat.Count) % flat.Count];
                    double[] o = flat[si];
                    double[] b = flat[(si + 1) % flat.Count];
                    return SquareAlgorithm.CalcMotion(o, a, b, true, mi, simplified.Count);
                })];

                for (int mi = 0; mi < simplified.Count; mi++)
                {
                    flat[simplified[mi]][0] += motions[mi][0];
                    flat[simplified[mi]][1] += motions[mi][1];
                }
            }

            double newScore = projected.Sum(r => SquareAlgorithm.CalcScore(r.Flat, r.Simplified, true));
            if (newScore < score)
            {
                score = newScore;
            }

            if (score < SquareAlgorithm.Epsilon)
            {
                break;
            }
        }

        // Reassemble: unproject squared exterior rings and keep original interior rings per polygon.
        bool anyMoved = false;
        List<Polygon> squaredPolygons = [];
        for (int pi = 0; pi < coordRings.Count; pi++)
        {
            List<double[]> flat = projected[pi].Flat;
            List<double[]> original = projected[pi].Original;
            int count = coordRings[pi].Count;

            Coordinate[] outRing = new Coordinate[count + 1];
            for (int i = 0; i < count; i++)
            {
                double dx = flat[i][0] - original[i][0];
                double dy = flat[i][1] - original[i][1];
                if (Math.Sqrt(dx * dx + dy * dy) >= SquareAlgorithm.Epsilon)
                {
                    anyMoved = true;
                }

                outRing[i] = GeometryService.Unproject(new Coordinate(flat[i][0], flat[i][1]), latRef);
            }

            outRing[count] = outRing[0];
            LinearRing[] holes = [.. polygons[pi].InteriorRings.Cast<LinearRing>()];
            squaredPolygons.Add(factory.CreatePolygon(factory.CreateLinearRing(outRing), holes));
        }

        if (!anyMoved)
        {
            return Task.FromResult(QueryResult<IFeature>.Fail("No nodes need to move."));
        }

        MultiPolygon result = factory.CreateMultiPolygon([.. squaredPolygons]);
        return Task.FromResult(QueryResult<IFeature>.Pass(new Feature(result, CopyAttributes(request.Feature.Attributes))));
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
