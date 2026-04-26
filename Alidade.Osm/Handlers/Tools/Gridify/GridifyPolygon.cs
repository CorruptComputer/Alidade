using Alidade.Osm.Models.Tools.Gridify;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Gridify;

/// <inheritdoc />
public sealed class GridifyPolygon(GeometryFactory factory) : IRequestHandler<GridifyPolygon.Query, QueryResult<IReadOnlyList<IFeature>>>
{
    /// <summary>
    ///   Splits a polygon feature into a grid of smaller polygon features using the configuration
    ///   in <paramref name="State"/>. Each output feature receives a full copy of the input
    ///   feature's attributes. <see cref="GridifyState.WayId"/> is not used by this handler.
    /// </summary>
    /// <param name="Feature">
    ///   The polygon feature to split. <see cref="IFeature.Geometry"/> must be a
    ///   <see cref="Polygon"/>. Coordinates are expected in WGS-84 (X = longitude, Y = latitude).
    /// </param>
    /// <param name="State">
    ///   Gridify configuration supplying row/column counts and rotation angles.
    /// </param>
    public record Query(IFeature Feature, GridifyState State)
        : IRequest<QueryResult<IReadOnlyList<IFeature>>>;

    /// <inheritdoc />
    public Task<QueryResult<IReadOnlyList<IFeature>>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (request.Feature.Geometry is not Polygon polygon)
        {
            return Task.FromResult(QueryResult<IReadOnlyList<IFeature>>.Fail("Feature geometry must be a Polygon."));
        }

        GridifyState state = request.State;

        if (state.Rows < 1 || state.Cols < 1)
        {
            return Task.FromResult(QueryResult<IReadOnlyList<IFeature>>.Fail("Rows and columns must be at least 1."));
        }

        // Centroid Y = latitude reference for the flat projection (NTS: X = lon, Y = lat).
        double latRef = polygon.Centroid.Y;

        // Build the skewed coordinate frame. colRotationDeg/rowRotationDeg are swapped from
        // the UI extension angles to align the OBB with the polygon for any parallelogram input
        // (same convention as GridifyWay.Compute).
        double colRad = state.RowRotationDeg * Math.PI / 180.0;
        double rowRad = state.ColRotationDeg * Math.PI / 180.0;
        double cosC = Math.Cos(colRad), sinC = Math.Sin(colRad);
        double cosR = Math.Cos(rowRad), sinR = Math.Sin(rowRad);
        double det = cosC * sinR - cosR * sinC;

        if (Math.Abs(det) < 1e-6)
        {
            return Task.FromResult(QueryResult<IReadOnlyList<IFeature>>.Fail("Row and column axes are parallel."));
        }

        // Project the exterior ring into the skewed coordinate frame.
        Coordinate[] ring = polygon.ExteriorRing.Coordinates;
        List<(double X, double Y)> skewedPts = [.. ring.Take(ring.Length - 1).Select(coord =>
        {
            Coordinate flat = GeometryService.Project(coord, latRef);
            return ((sinR * flat.X - cosR * flat.Y) / det, (cosC * flat.Y - sinC * flat.X) / det);
        })];

        double minX = skewedPts.Min(p => p.X);
        double maxX = skewedPts.Max(p => p.X);
        double minY = skewedPts.Min(p => p.Y);
        double maxY = skewedPts.Max(p => p.Y);
        double width = maxX - minX;
        double height = maxY - minY;

        if (width < 1e-6 || height < 1e-6)
        {
            return Task.FromResult(QueryResult<IReadOnlyList<IFeature>>.Fail("Polygon is degenerate."));
        }

        List<IFeature> cells = [];

        for (int r = 0; r < state.Rows; r++)
        {
            double ty0 = (double)r / state.Rows;
            double ty1 = (double)(r + 1) / state.Rows;

            for (int c = 0; c < state.Cols; c++)
            {
                double tx0 = (double)c / state.Cols;
                double tx1 = (double)(c + 1) / state.Cols;

                // Four corners of the cell in skewed space (counter-clockwise).
                (double X, double Y)[] corners =
                [
                    (minX + tx0 * width, minY + ty0 * height),
                    (minX + tx1 * width, minY + ty0 * height),
                    (minX + tx1 * width, minY + ty1 * height),
                    (minX + tx0 * width, minY + ty1 * height),
                ];

                Coordinate[] cellCoords = new Coordinate[5];
                for (int k = 0; k < 4; k++)
                {
                    double px = corners[k].X * cosC + corners[k].Y * cosR;
                    double py = corners[k].X * sinC + corners[k].Y * sinR;
                    cellCoords[k] = GeometryService.Unproject(new Coordinate(px, py), latRef);
                }
                cellCoords[4] = cellCoords[0];

                Polygon cellPolygon = factory.CreatePolygon(cellCoords);
                Geometry intersection = polygon.Intersection(cellPolygon);

                if (intersection.IsEmpty)
                {
                    continue;
                }

                // Intersection may return Polygon or MultiPolygon for non-convex inputs.
                IEnumerable<Geometry> parts = intersection is GeometryCollection gc
                    ? gc.Geometries
                    : [intersection];

                foreach (Geometry part in parts)
                {
                    if (part is Polygon cellResult && !cellResult.IsEmpty)
                    {
                        cells.Add(new Feature(cellResult, CopyAttributes(request.Feature.Attributes)));
                    }
                }
            }
        }

        return cells.Count == 0
            ? Task.FromResult(QueryResult<IReadOnlyList<IFeature>>.Fail("No cells intersect the polygon."))
            : Task.FromResult<QueryResult<IReadOnlyList<IFeature>>>(cells);
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
