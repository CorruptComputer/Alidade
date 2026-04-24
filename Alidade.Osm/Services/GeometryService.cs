namespace Alidade.Osm.Services;

/// <summary>
///   Geometric operations: orthogonalization, circularization, segment math.
///   Works in a local flat-Earth projection (Web Mercator approximation) to preserve
///   distance relationships. Results are returned as coordinate moves to dispatch as actions.
/// </summary>
public static class GeometryService
{
    private const double DegThreshold = 13.0; // degrees within 90° to correct
    private const double Epsilon = 1e-4;
    private const int MaxIterations = 1000;

    private static readonly double LowerThreshold =
        Math.Cos((90.0 - DegThreshold) * Math.PI / 180.0);
    private static readonly double UpperThreshold =
        Math.Cos(DegThreshold * Math.PI / 180.0);

    #region  Projection helpers
    // Project lat/lon to a local flat coordinate system (units: approximate meters)
    private static (double X, double Y) Project(double lat, double lon, double latRef)
    {
        const double R = 6378137.0;
        double cosLat = Math.Cos(latRef * Math.PI / 180.0);
        return (lon * Math.PI / 180.0 * R * cosLat, lat * Math.PI / 180.0 * R);
    }

    private static (double Lat, double Lon) Unproject(double x, double y, double latRef)
    {
        const double R = 6378137.0;
        double cosLat = Math.Cos(latRef * Math.PI / 180.0);
        return (y / (Math.PI / 180.0 * R), x / (Math.PI / 180.0 * R * cosLat));
    }
    #endregion

    #region Orthogonalization
    /// <summary>
    ///   Computes node moves that square the corners of a way toward the nearest 90° increment.
    ///   Implements the iterative bisector-motion algorithm from iD's <c>actionOrthogonalize</c>.
    ///   Returns the list of (nodeId, oldLat, oldLon, newLat, newLon) moves to dispatch.
    /// </summary>
    /// <param name="wayId">The ID of the way to orthogonalize.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <returns>
    ///   A list of (NodeId, OldLat, OldLon, NewLat, NewLon) tuples for each node that moves.
    ///   Returns an empty list when the way cannot be orthogonalized.
    /// </returns>
    public static IReadOnlyList<(long NodeId, double OldLat, double OldLon, double NewLat, double NewLon)>
        Orthogonalize(long wayId, ImmutableDictionary<long, OsmWay> ways,
            ImmutableDictionary<long, OsmNode> nodes)
    {
        if (!ways.TryGetValue(wayId, out OsmWay? way))
        {
            return [];
        }

        List<long> nodeIds = way.IsClosed
            ? [.. way.NodeIds.Take(way.NodeIds.Count - 1)]
            : [.. way.NodeIds];

        if (nodeIds.Count < 3)
        {
            return [];
        }

        List<OsmNode> nodeList = [.. nodeIds
            .Where(nodes.ContainsKey)
            .Select(id => nodes[id])];

        if (nodeList.Count < 3)
        {
            return [];
        }

        double latRef = nodeList.Average(n => n.Lat);

        // Project to local flat space
        List<double[]> pts = [.. nodeList.Select(n =>
        {
            (double x, double y) = Project(n.Lat, n.Lon, latRef);
            return new double[] { x, y };
        })];

        List<double[]> original = [.. pts.Select(p => new double[] { p[0], p[1] })];
        bool isClosed = way.IsClosed;

        // Separate nearly-straight nodes from corner nodes
        List<int> straights = [];
        List<int> simplified = []; // indices into pts

        for (int i = 0; i < pts.Count; i++)
        {
            double dotp = 0;
            if (isClosed || (i > 0 && i < pts.Count - 1))
            {
                int prev = (i - 1 + pts.Count) % pts.Count;
                int next = (i + 1) % pts.Count;
                dotp = Math.Abs(NormalizedDot(pts[prev], pts[next], pts[i]));
            }

            if (dotp > UpperThreshold)
            {
                straights.Add(i);
            }
            else
            {
                simplified.Add(i);
            }
        }

        // Run iterative orthogonalization on simplified subset
        double score = double.MaxValue;
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            List<double[]> motions = [.. simplified.Select((si, mi) =>
            {
                double[] a = pts[(si - 1 + pts.Count) % pts.Count];
                double[] o = pts[si];
                double[] b = pts[(si + 1) % pts.Count];
                return CalcMotion(o, a, b, isClosed, mi, simplified.Count);
            })];

            for (int mi = 0; mi < simplified.Count; mi++)
            {
                int si = simplified[mi];
                pts[si][0] += motions[mi][0];
                pts[si][1] += motions[mi][1];
            }

            double newScore = CalcScore(pts, simplified, isClosed);
            if (newScore < score)
            {
                score = newScore;
            }

            if (score < Epsilon)
            {
                break;
            }
        }

        // Build move list for changed nodes
        List<(long NodeId, double OldLat, double OldLon, double NewLat, double NewLon)> moves = [];
        for (int i = 0; i < nodeList.Count; i++)
        {
            OsmNode node = nodeList[i];
            double dx = pts[i][0] - original[i][0];
            double dy = pts[i][1] - original[i][1];
            if (Math.Sqrt(dx * dx + dy * dy) < Epsilon)
            {
                continue;
            }

            (double newLat, double newLon) = Unproject(pts[i][0], pts[i][1], latRef);
            moves.Add((node.Id, node.Lat, node.Lon, newLat, newLon));
        }

        return moves;
    }
    #endregion

    #region Circularization
    /// <summary>
    ///   Computes node moves that fit a closed way to its best-fit circle by projecting each
    ///   node onto the circle defined by the centroid and average radius.
    ///   Returns the list of (nodeId, oldLat, oldLon, newLat, newLon) moves to dispatch.
    /// </summary>
    /// <param name="wayId">The ID of the closed way to circularize.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <returns>
    ///   A list of (NodeId, OldLat, OldLon, NewLat, NewLon) tuples for each node that moves.
    ///   Returns an empty list when the way cannot be circularized.
    /// </returns>
    public static IReadOnlyList<(long NodeId, double OldLat, double OldLon, double NewLat, double NewLon)>
        Circularize(long wayId, ImmutableDictionary<long, OsmWay> ways,
            ImmutableDictionary<long, OsmNode> nodes)
    {
        if (!ways.TryGetValue(wayId, out OsmWay? way) || !way.IsClosed)
        {
            return [];
        }

        List<long> nodeIds = [.. way.NodeIds.Take(way.NodeIds.Count - 1)];
        List<OsmNode> nodeList = [.. nodeIds.Where(nodes.ContainsKey).Select(id => nodes[id])];
        if (nodeList.Count < 3)
        {
            return [];
        }

        double latRef = nodeList.Average(n => n.Lat);
        List<(double X, double Y)> pts = [.. nodeList.Select(n => Project(n.Lat, n.Lon, latRef))];

        // Compute centroid and average radius
        double cx = pts.Average(p => p.X);
        double cy = pts.Average(p => p.Y);
        double r = pts.Average(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)));

        List<(long, double, double, double, double)> moves = [];
        for (int i = 0; i < nodeList.Count; i++)
        {
            double angle = Math.Atan2(pts[i].Y - cy, pts[i].X - cx);
            double newX = cx + r * Math.Cos(angle);
            double newY = cy + r * Math.Sin(angle);
            (double newLat, double newLon) = Unproject(newX, newY, latRef);
            moves.Add((nodeList[i].Id, nodeList[i].Lat, nodeList[i].Lon, newLat, newLon));
        }

        return moves;
    }
    #endregion

    #region Segment geometry
    /// <summary>
    ///   Finds crossing segment pairs in the edit buffer for validation.
    ///   Returns pairs of (wayId1, wayId2) for ways that cross without a shared node,
    ///   filtered to ways carrying at least one tag from <paramref name="relevantTags"/>.
    /// </summary>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <param name="relevantTags">
    ///   The set of OSM tag keys to filter by (e.g. <c>highway</c>, <c>waterway</c>).
    /// </param>
    /// <returns>Pairs of (WayId1, WayId2) for crossing ways.</returns>
    public static IEnumerable<(long WayId1, long WayId2)> FindCrossingWays(
        ImmutableDictionary<long, OsmWay> ways,
        ImmutableDictionary<long, OsmNode> nodes,
        ISet<string> relevantTags)
    {
        List<OsmWay> relevant = [.. ways.Values.Where(w => w.Tags.Keys.Any(relevantTags.Contains))];

        for (int i = 0; i < relevant.Count; i++)
        {
            for (int j = i + 1; j < relevant.Count; j++)
            {
                if (WaysCross(relevant[i], relevant[j], nodes))
                {
                    yield return (relevant[i].Id, relevant[j].Id);
                }
            }
        }
    }

    private static bool WaysCross(OsmWay a, OsmWay b, ImmutableDictionary<long, OsmNode> nodes)
    {
        HashSet<long> aNodes = new(a.NodeIds);
        for (int i = 0; i < a.NodeIds.Count - 1; i++)
        {
            if (!nodes.TryGetValue(a.NodeIds[i], out OsmNode? a1)
                || !nodes.TryGetValue(a.NodeIds[i + 1], out OsmNode? a2))
            {
                continue;
            }

            for (int j = 0; j < b.NodeIds.Count - 1; j++)
            {
                // Skip segments that share a node
                if (aNodes.Contains(b.NodeIds[j]) || aNodes.Contains(b.NodeIds[j + 1]))
                {
                    continue;
                }

                if (!nodes.TryGetValue(b.NodeIds[j], out OsmNode? b1)
                    || !nodes.TryGetValue(b.NodeIds[j + 1], out OsmNode? b2))
                {
                    continue;
                }

                if (SegmentsIntersect(a1.Lon, a1.Lat, a2.Lon, a2.Lat,
                                      b1.Lon, b1.Lat, b2.Lon, b2.Lat))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(
        double x1, double y1, double x2, double y2,
        double x3, double y3, double x4, double y4)
    {
        double d1x = x2 - x1, d1y = y2 - y1;
        double d2x = x4 - x3, d2y = y4 - y3;
        double denom = d1x * d2y - d1y * d2x;
        if (Math.Abs(denom) < 1e-10)
        {
            return false; // parallel
        }

        double t = ((x3 - x1) * d2y - (y3 - y1) * d2x) / denom;
        double u = ((x3 - x1) * d1y - (y3 - y1) * d1x) / denom;

        return t > 0 && t < 1 && u > 0 && u < 1;
    }
    #endregion

    #region Math helpers
    private static double[] CalcMotion(double[] origin, double[] a, double[] b,
        bool isClosed, int i, int count)
    {
        if (!isClosed && (i == 0 || i == count - 1))
        {
            return [0, 0];
        }

        double px = a[0] - origin[0], py = a[1] - origin[1];
        double qx = b[0] - origin[0], qy = b[1] - origin[1];
        double pLen = Math.Sqrt(px * px + py * py);
        double qLen = Math.Sqrt(qx * qx + qy * qy);
        if (pLen < 1e-10 || qLen < 1e-10)
        {
            return [0, 0];
        }

        double scale = 2.0 * Math.Min(pLen, qLen);
        px /= pLen; py /= pLen;
        qx /= qLen; qy /= qLen;

        double dotp = px * qx + py * qy;
        double val = Math.Abs(dotp);

        if (val < LowerThreshold)
        {
            double bx = px + qx, by = py + qy;
            double bLen = Math.Sqrt(bx * bx + by * by);
            if (bLen < 1e-10)
            {
                return [0, 0];
            }

            bx /= bLen; by /= bLen;
            return [bx * 0.1 * dotp * scale, by * 0.1 * dotp * scale];
        }

        return [0, 0];
    }

    private static double NormalizedDot(double[] a, double[] b, double[] o)
    {
        double ax = a[0] - o[0], ay = a[1] - o[1];
        double bx = b[0] - o[0], by = b[1] - o[1];
        double aLen = Math.Sqrt(ax * ax + ay * ay);
        double bLen = Math.Sqrt(bx * bx + by * by);
        if (aLen < 1e-10 || bLen < 1e-10)
        {
            return 0;
        }

        return (ax / aLen * bx / bLen) + (ay / aLen * by / bLen);
    }

    private static double CalcScore(List<double[]> pts, List<int> simplified, bool isClosed)
    {
        double score = 0;
        for (int mi = 0; mi < simplified.Count; mi++)
        {
            if (!isClosed && (mi == 0 || mi == simplified.Count - 1))
            {
                continue;
            }

            int si = simplified[mi];
            int prevSi = simplified[(mi - 1 + simplified.Count) % simplified.Count];
            int nextSi = simplified[(mi + 1) % simplified.Count];
            double dotp = Math.Abs(NormalizedDot(pts[prevSi], pts[nextSi], pts[si]));
            score += dotp < LowerThreshold ? dotp : 0;
        }

        return score;
    }
    #endregion

    #region Gridify
    /// <summary>
    ///   Computes the grid vertices and cell node rings needed to split a closed way into
    ///   <paramref name="rows"/> × <paramref name="cols"/> equal rectangular sub-areas.
    ///   The grid axes are rotated by <paramref name="rotationDeg"/> degrees (clockwise from east).
    ///   Nodes on the outer boundary of the original way are reused rather than replaced, so
    ///   connections to adjacent ways are preserved.
    ///   Returns <see cref="GridifyResult.Empty"/> when the way cannot be gridified.
    /// </summary>
    /// <param name="wayId">The ID of the closed way to split.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <param name="rows">Number of rows in the output grid.</param>
    /// <param name="cols">Number of columns in the output grid.</param>
    /// <param name="rotationDeg">Grid rotation in degrees (clockwise from east in projected space).</param>
    /// <returns>
    ///   A <see cref="GridifyResult"/> with new node positions and per-cell closed rings that
    ///   reference either new or existing nodes. Returns <see cref="GridifyResult.Empty"/> on failure.
    /// </returns>
    public static GridifyResult Gridify(long wayId, ImmutableDictionary<long, OsmWay> ways, ImmutableDictionary<long, OsmNode> nodes, int rows, int cols, double rotationDeg)
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
        double rotRad = rotationDeg * Math.PI / 180.0;

        // Project all original nodes into rotated flat space for OBB computation
        List<(double X, double Y)> rotatedPts = [.. nodeList.Select(n =>
        {
            (double px, double py) = Project(n.Lat, n.Lon, latRef);
            return RotateXY(px, py, -rotRad);
        })];

        double minX = rotatedPts.Min(p => p.X);
        double maxX = rotatedPts.Max(p => p.X);
        double minY = rotatedPts.Min(p => p.Y);
        double maxY = rotatedPts.Max(p => p.Y);
        double width = maxX - minX;
        double height = maxY - minY;

        if (width < 1e-6 || height < 1e-6)
        {
            return GridifyResult.Empty;
        }

        // TODO: for curved or non-convex ways, clip each cell polygon to the source way boundary
        //       using NTS Intersection before returning cell node refs.

        // Classify every original node onto the OBB edges it lies on (nodes can be on two edges at
        // a corner). edgeEpsilon is 2% of the shorter extent, which handles floating-point imprecision
        // on orthogonalized ways while avoiding false positives on interior nodes.
        double edgeEpsilon = Math.Min(width, height) * 0.02;

        // Each entry is (normalized position along edge [0..1], original node ID).
        // Left/right edges are parameterized by ty = (ry - minY) / height.
        // Top/bottom edges are parameterized by tx = (rx - minX) / width.
        List<(double T, long NodeId)> leftEdge = [];
        List<(double T, long NodeId)> rightEdge = [];
        List<(double T, long NodeId)> topEdge = [];
        List<(double T, long NodeId)> bottomEdge = [];

        for (int i = 0; i < nodeList.Count; i++)
        {
            (double rx, double ry) = rotatedPts[i];
            long nid = nodeList[i].Id;
            double tx = (rx - minX) / width;
            double ty = (ry - minY) / height;

            if (Math.Abs(rx - minX) <= edgeEpsilon)
            {
                leftEdge.Add((ty, nid));
            }

            if (Math.Abs(rx - maxX) <= edgeEpsilon)
            {
                rightEdge.Add((ty, nid));
            }

            if (Math.Abs(ry - minY) <= edgeEpsilon)
            {
                topEdge.Add((tx, nid));
            }

            if (Math.Abs(ry - maxY) <= edgeEpsilon)
            {
                bottomEdge.Add((tx, nid));
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
        GridifyNodeRef[,] gridRef  = new GridifyNodeRef[vRows, vCols];
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
                    (double ux, double uy) = RotateXY(minX + tx * width, minY + ty * height, rotRad);
                    (double lat, double lon) = Unproject(ux, uy, latRef);
                    gridRef[r, c] = GridifyNodeRef.New(newNodes.Count);
                    newNodes.Add((lat, lon));
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
    ///   Computes the angle (in degrees, 0–360 clockwise from east) of the longest edge of a way.
    ///   Useful for pre-populating the rotation field in the gridify dialog.
    ///   Returns 0.0 when the way cannot be resolved.
    /// </summary>
    /// <param name="wayId">The ID of the way to inspect.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <returns>The longest-edge angle in degrees, normalized to [0, 360).</returns>
    public static double ComputeLongestEdgeAngleDeg(
        long wayId,
        ImmutableDictionary<long, OsmWay> ways,
        ImmutableDictionary<long, OsmNode> nodes)
    {
        if (!ways.TryGetValue(wayId, out OsmWay? way))
        {
            return 0.0;
        }

        List<long> nodeIds = way.IsClosed
            ? [.. way.NodeIds.Take(way.NodeIds.Count - 1)]
            : [.. way.NodeIds];

        if (nodeIds.Count < 2)
        {
            return 0.0;
        }

        double latRef = nodeIds
            .Where(nodes.ContainsKey)
            .Select(id => nodes[id].Lat)
            .DefaultIfEmpty(0.0)
            .Average();

        double longestLen = 0.0;
        double longestAngle = 0.0;

        for (int i = 0; i < nodeIds.Count; i++)
        {
            int j = (i + 1) % nodeIds.Count;
            if (!nodes.TryGetValue(nodeIds[i], out OsmNode? a))
            {
                continue;
            }

            if (!nodes.TryGetValue(nodeIds[j], out OsmNode? b))
            {
                continue;
            }

            (double ax, double ay) = Project(a.Lat, a.Lon, latRef);
            (double bx, double by) = Project(b.Lat, b.Lon, latRef);
            double dx = bx - ax;
            double dy = by - ay;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len > longestLen)
            {
                longestLen = len;
                longestAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }
        }

        return ((longestAngle % 360.0) + 360.0) % 360.0;
    }

    private static (double X, double Y) RotateXY(double x, double y, double rad)
    {
        double cos = Math.Cos(rad);
        double sin = Math.Sin(rad);
        return (x * cos - y * sin, x * sin + y * cos);
    }
    #endregion
}
