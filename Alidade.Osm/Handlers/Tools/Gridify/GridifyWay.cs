using Alidade.Osm.Models.Tools.Gridify;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Gridify;

/// <inheritdoc />
public sealed class GridifyWay(EditBufferStateService editBufferState)
    : IRequestHandler<GridifyWay.Query, QueryResult<GridifyResult>>
{
    /// <summary>
    ///   Computes the grid vertices and cell node rings needed to split a closed way into
    ///   <see cref="GridifyState.Rows"/> × <see cref="GridifyState.Cols"/> sub-areas.
    ///   Column lines run at the row extension angle and row lines at the column extension angle
    ///   (the swap aligns the OBB with the polygon for any parallelogram input).
    ///   Nodes on the outer boundary of the original way are reused rather than replaced, so
    ///   connections to adjacent ways are preserved.
    /// </summary>
    /// <param name="State">The gridify configuration to compute a result for.</param>
    public record Query(GridifyState State) : IRequest<QueryResult<GridifyResult>>;

    /// <inheritdoc />
    public Task<QueryResult<GridifyResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        GridifyState state = request.State;

        if (state.WayId is not long wayId)
        {
            return Task.FromResult(QueryResult<GridifyResult>.Fail("No way selected."));
        }

        EditBufferState buf = editBufferState.State;
        GridifyResult result = Compute(wayId, buf.Ways, buf.Nodes, state.Rows, state.Cols,
            state.RowRotationDeg, state.ColRotationDeg);

        return result.CellNodeRefs.Count == 0
            ? Task.FromResult(QueryResult<GridifyResult>.Fail("The selected way cannot be gridified. Select a single closed way."))
            : Task.FromResult<QueryResult<GridifyResult>>(result);
    }

    // ----- Geometry -------------------------------------------------------

    private static GridifyResult Compute(
        long wayId,
        ImmutableDictionary<long, OsmWay> ways,
        ImmutableDictionary<long, OsmNode> nodes,
        int rows, int cols,
        double colRotationDeg, double rowRotationDeg)
    {
        if (rows < 1 || cols < 1)
        {
            return GridifyResult.Empty;
        }

        if (!ways.TryGetValue(wayId, out OsmWay? way) || !way.IsClosed)
        {
            return GridifyResult.Empty;
        }

        List<long> nodeIds = [.. way.NodeIds.Take(way.NodeIds.Count - 1)];
        if (nodeIds.Count < 3)
        {
            return GridifyResult.Empty;
        }

        List<OsmNode> nodeList = [.. nodeIds.Where(nodes.ContainsKey).Select(id => nodes[id])];
        if (nodeList.Count < 3)
        {
            return GridifyResult.Empty;
        }

        double latRef = nodeList.Average(n => n.Lat);

        // Build the skewed coordinate frame from the two independent axis angles.
        // sx is the coordinate along the column axis, sy along the row axis.
        // Forward transform (skewed → flat): px = sx*cosC + sy*cosR, py = sx*sinC + sy*sinR
        // Inverse transform (flat → skewed):
        //   sx = ( sinR*px − cosR*py) / det
        //   sy = ( cosC*py − sinC*px) / det
        // where det = cosC*sinR − cosR*sinC (= 1 when axes are orthogonal).
        double colRad = colRotationDeg * Math.PI / 180.0;
        double rowRad = rowRotationDeg * Math.PI / 180.0;
        double cosC = Math.Cos(colRad), sinC = Math.Sin(colRad);
        double cosR = Math.Cos(rowRad), sinR = Math.Sin(rowRad);
        double det = cosC * sinR - cosR * sinC;

        if (Math.Abs(det) < 1e-6)
        {
            return GridifyResult.Empty;
        }

        // Project all original nodes into the skewed column/row coordinate frame.
        List<(double X, double Y)> skewedPts = [.. nodeList.Select(n =>
        {
            Coordinate flat = GeometryService.Project(new Coordinate(n.Lon, n.Lat), latRef);
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
            return GridifyResult.Empty;
        }

        // Use the OBB corners as the bilinear reference frame. For the auto-detected
        // natural angle these coincide with the polygon corners; for adjusted angles
        // the grid extends into the OBB, which is what makes rotation visibly effective.
        // Snapping to existing polygon nodes (below) preserves boundary connectivity.
        (double X, double Y) c00 = (minX, minY);
        (double X, double Y) c10 = (maxX, minY);
        (double X, double Y) c11 = (maxX, maxY);
        (double X, double Y) c01 = (minX, maxY);

        // TODO: for curved or non-convex ways, clip each cell polygon to the source way boundary
        //       using NTS Intersection before returning cell node refs.

        // Classify every original node onto the 4 polygon boundary edges it lies on.
        // edgeEpsilon is 2% of the shorter OBB extent: tolerant enough for near-orthogonal
        // ways while avoiding false positives on interior nodes of skewed polygons.
        double edgeEpsilon = Math.Min(width, height) * 0.02;

        // Each entry is (normalized position along edge [0..1], original node ID).
        // Left edge:   c00 → c01  (tx=0 side)
        // Right edge:  c10 → c11  (tx=1 side)
        // Top edge:    c00 → c10  (ty=0 side)
        // Bottom edge: c01 → c11  (ty=1 side)
        List<(double T, long NodeId)> leftEdge = [];
        List<(double T, long NodeId)> rightEdge = [];
        List<(double T, long NodeId)> topEdge = [];
        List<(double T, long NodeId)> bottomEdge = [];

        for (int i = 0; i < nodeList.Count; i++)
        {
            (double rx, double ry) = skewedPts[i];
            long nid = nodeList[i].Id;

            if (DistanceToSegmentLine(c00, c01, rx, ry) <= edgeEpsilon)
            {
                leftEdge.Add((ProjectOntoSegment(c00, c01, rx, ry), nid));
            }

            if (DistanceToSegmentLine(c10, c11, rx, ry) <= edgeEpsilon)
            {
                rightEdge.Add((ProjectOntoSegment(c10, c11, rx, ry), nid));
            }

            if (DistanceToSegmentLine(c00, c10, rx, ry) <= edgeEpsilon)
            {
                topEdge.Add((ProjectOntoSegment(c00, c10, rx, ry), nid));
            }

            if (DistanceToSegmentLine(c01, c11, rx, ry) <= edgeEpsilon)
            {
                bottomEdge.Add((ProjectOntoSegment(c01, c11, rx, ry), nid));
            }
        }

        leftEdge.Sort((a, b)   => a.T.CompareTo(b.T));
        rightEdge.Sort((a, b)  => a.T.CompareTo(b.T));
        topEdge.Sort((a, b)    => a.T.CompareTo(b.T));
        bottomEdge.Sort((a, b) => a.T.CompareTo(b.T));

        // How close a grid vertex's normalized position must be to an original node to reuse it.
        // 1e-4 in normalized space (0.01%) is tight enough to avoid false snapping.
        const double SnapEpsilonT = 1e-4;

        // Build the (rows+1) × (cols+1) grid of node references.
        // Boundary vertices reuse original nodes where found; all others get new nodes.
        int vRows = rows + 1;
        int vCols = cols + 1;
        GridifyNodeRef[,] gridRef = new GridifyNodeRef[vRows, vCols];
        List<(double Lat, double Lon)> newNodes = [];

        for (int r = 0; r < vRows; r++)
        {
            double ty = (double)r / rows;
            for (int c = 0; c < vCols; c++)
            {
                double tx = (double)c / cols;
                long? existingId = FindGridVertexNode(
                    r, c, rows, cols, tx, ty,
                    leftEdge, rightEdge, topEdge, bottomEdge, SnapEpsilonT);

                if (existingId.HasValue)
                {
                    gridRef[r, c] = GridifyNodeRef.Existing(existingId.Value);
                }
                else
                {
                    (double gx, double gy) = BilinearInterp(c00, c10, c01, c11, tx, ty);
                    double ux = gx * cosC + gy * cosR;
                    double uy = gx * sinC + gy * sinR;
                    Coordinate latLon = GeometryService.Unproject(new Coordinate(ux, uy), latRef);
                    gridRef[r, c] = GridifyNodeRef.New(newNodes.Count);
                    newNodes.Add((latLon.Y, latLon.X));
                }
            }
        }

        // Build cell rings. Boundary cells also include any original edge nodes that fall strictly
        // between the two grid vertex positions on that edge, preserving mid-edge connections.
        List<IReadOnlyList<GridifyNodeRef>> cells = new List<IReadOnlyList<GridifyNodeRef>>(rows * cols);

        for (int r = 0; r < rows; r++)
        {
            double ty0 = (double)r       / rows;
            double ty1 = (double)(r + 1) / rows;

            for (int c = 0; c < cols; c++)
            {
                double tx0 = (double)c       / cols;
                double tx1 = (double)(c + 1) / cols;

                List<GridifyNodeRef> ring =
                [
                    // TL corner → traverse top edge → TR corner
                    gridRef[r, c],
                ];

                if (r == 0)
                {
                    AppendIntermediateEdgeNodes(ring, topEdge, tx0, tx1, SnapEpsilonT);
                }
                ring.Add(gridRef[r, c + 1]);

                // TR corner → traverse right edge → BR corner
                if (c == cols - 1)
                {
                    AppendIntermediateEdgeNodes(ring, rightEdge, ty0, ty1, SnapEpsilonT);
                }
                ring.Add(gridRef[r + 1, c + 1]);

                // BR corner → traverse bottom edge (right-to-left) → BL corner
                if (r == rows - 1)
                {
                    AppendIntermediateEdgeNodesReverse(ring, bottomEdge, tx0, tx1, SnapEpsilonT);
                }
                ring.Add(gridRef[r + 1, c]);

                // BL corner → traverse left edge (bottom-to-top) → close at TL
                if (c == 0)
                {
                    AppendIntermediateEdgeNodesReverse(ring, leftEdge, ty0, ty1, SnapEpsilonT);
                }

                ring.Add(ring[0]); // close ring
                cells.Add(ring);
            }
        }

        return new GridifyResult(newNodes, cells);
    }

    /// <summary>
    ///   Returns the ID of an original way node that lies at the given normalised grid-vertex
    ///   position <paramref name="tx"/>, <paramref name="ty"/>, or <see langword="null"/> if no
    ///   original node is close enough to snap.
    /// </summary>
    /// <param name="r">Grid row index.</param>
    /// <param name="c">Grid column index.</param>
    /// <param name="rows">Total row count.</param>
    /// <param name="cols">Total column count.</param>
    /// <param name="tx">Normalised horizontal position [0, 1].</param>
    /// <param name="ty">Normalised vertical position [0, 1].</param>
    /// <param name="leftEdge">Original nodes on the left OBB edge, sorted by T.</param>
    /// <param name="rightEdge">Original nodes on the right OBB edge, sorted by T.</param>
    /// <param name="topEdge">Original nodes on the top OBB edge, sorted by T.</param>
    /// <param name="bottomEdge">Original nodes on the bottom OBB edge, sorted by T.</param>
    /// <param name="epsilon">Match tolerance in normalised [0, 1] space.</param>
    /// <returns>The matching original node ID, or <see langword="null"/>.</returns>
    private static long? FindGridVertexNode(
        int r, int c, int rows, int cols, double tx, double ty,
        List<(double T, long NodeId)> leftEdge,
        List<(double T, long NodeId)> rightEdge,
        List<(double T, long NodeId)> topEdge,
        List<(double T, long NodeId)> bottomEdge,
        double epsilon)
    {
        if (c == 0)
        {
            foreach ((double t, long id) in leftEdge)
            {
                if (Math.Abs(t - ty) < epsilon) return id;
            }
        }

        if (c == cols)
        {
            foreach ((double t, long id) in rightEdge)
            {
                if (Math.Abs(t - ty) < epsilon) return id;
            }
        }

        if (r == 0)
        {
            foreach ((double t, long id) in topEdge)
            {
                if (Math.Abs(t - tx) < epsilon) return id;
            }
        }

        if (r == rows)
        {
            foreach ((double t, long id) in bottomEdge)
            {
                if (Math.Abs(t - tx) < epsilon) return id;
            }
        }

        return null;
    }

    /// <summary>
    ///   Appends original edge nodes whose normalised position is strictly between
    ///   <paramref name="t0"/> and <paramref name="t1"/> (ascending order).
    /// </summary>
    /// <param name="ring">The cell ring being built.</param>
    /// <param name="edgeList">Sorted edge node list.</param>
    /// <param name="t0">Start of the range (exclusive).</param>
    /// <param name="t1">End of the range (exclusive).</param>
    /// <param name="epsilon">Exclusion tolerance at the endpoints.</param>
    private static void AppendIntermediateEdgeNodes(
        List<GridifyNodeRef> ring,
        List<(double T, long NodeId)> edgeList,
        double t0, double t1, double epsilon)
    {
        foreach ((double t, long nodeId) in edgeList)
        {
            if (t > t0 + epsilon && t < t1 - epsilon)
            {
                ring.Add(GridifyNodeRef.Existing(nodeId));
            }
        }
    }

    /// <summary>
    ///   Appends original edge nodes whose normalised position is strictly between
    ///   <paramref name="t0"/> and <paramref name="t1"/>, in descending order (for
    ///   edges traversed right-to-left or bottom-to-top).
    /// </summary>
    /// <param name="ring">The cell ring being built.</param>
    /// <param name="edgeList">Sorted edge node list.</param>
    /// <param name="t0">Start of the range (exclusive).</param>
    /// <param name="t1">End of the range (exclusive).</param>
    /// <param name="epsilon">Exclusion tolerance at the endpoints.</param>
    private static void AppendIntermediateEdgeNodesReverse(
        List<GridifyNodeRef> ring,
        List<(double T, long NodeId)> edgeList,
        double t0, double t1, double epsilon)
    {
        for (int i = edgeList.Count - 1; i >= 0; i--)
        {
            (double t, long nodeId) = edgeList[i];
            if (t > t0 + epsilon && t < t1 - epsilon)
            {
                ring.Add(GridifyNodeRef.Existing(nodeId));
            }
        }
    }

    /// <summary>
    ///   Returns the clamped projection parameter t ∈ [0, 1] of the point
    ///   (<paramref name="rx"/>, <paramref name="ry"/>) onto the directed segment
    ///   from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">Segment start.</param>
    /// <param name="to">Segment end.</param>
    /// <param name="rx">Point X coordinate.</param>
    /// <param name="ry">Point Y coordinate.</param>
    /// <returns>Normalised projection parameter in [0, 1].</returns>
    private static double ProjectOntoSegment((double X, double Y) from, (double X, double Y) to, double rx, double ry)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-20) return 0.0;
        return Math.Clamp(((rx - from.X) * dx + (ry - from.Y) * dy) / lenSq, 0.0, 1.0);
    }

    /// <summary>
    ///   Returns the perpendicular distance from the point
    ///   (<paramref name="rx"/>, <paramref name="ry"/>) to the infinite line
    ///   passing through <paramref name="from"/> and <paramref name="to"/>.
    /// </summary>
    /// <param name="from">First point on the line.</param>
    /// <param name="to">Second point on the line.</param>
    /// <param name="rx">Point X coordinate.</param>
    /// <param name="ry">Point Y coordinate.</param>
    /// <returns>Perpendicular distance (non-negative).</returns>
    private static double DistanceToSegmentLine((double X, double Y) from, (double X, double Y) to, double rx, double ry)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-10) return Math.Sqrt((rx - from.X) * (rx - from.X) + (ry - from.Y) * (ry - from.Y));
        return Math.Abs((ry - from.Y) * dx - (rx - from.X) * dy) / len;
    }

    /// <summary>
    ///   Bilinearly interpolates a point within the quadrilateral defined by four corners.
    ///   Corner convention: c00 = (tx=0, ty=0), c10 = (tx=1, ty=0),
    ///   c01 = (tx=0, ty=1), c11 = (tx=1, ty=1).
    /// </summary>
    /// <param name="c00">Corner at (tx=0, ty=0).</param>
    /// <param name="c10">Corner at (tx=1, ty=0).</param>
    /// <param name="c01">Corner at (tx=0, ty=1).</param>
    /// <param name="c11">Corner at (tx=1, ty=1).</param>
    /// <param name="tx">Normalised horizontal parameter [0, 1].</param>
    /// <param name="ty">Normalised vertical parameter [0, 1].</param>
    /// <returns>The interpolated (X, Y) position.</returns>
    private static (double X, double Y) BilinearInterp(
        (double X, double Y) c00, (double X, double Y) c10,
        (double X, double Y) c01, (double X, double Y) c11,
        double tx, double ty)
    {
        double w00 = (1 - tx) * (1 - ty);
        double w10 = tx       * (1 - ty);
        double w01 = (1 - tx) * ty;
        double w11 = tx       * ty;
        return (w00 * c00.X + w10 * c10.X + w01 * c01.X + w11 * c11.X,
                w00 * c00.Y + w10 * c10.Y + w01 * c01.Y + w11 * c11.Y);
    }
}
