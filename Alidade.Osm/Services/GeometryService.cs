using NetTopologySuite.Geometries;

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
    // Project a WGS-84 coordinate to a local flat system (units: approximate meters).
    // latLon follows NTS convention: X = longitude, Y = latitude.
    // Returns Coordinate(X = easting, Y = northing).
    internal static Coordinate Project(Coordinate latLon, double latRef)
    {
        const double R = 6378137.0;
        double cosLat = Math.Cos(latRef * Math.PI / 180.0);
        return new Coordinate(latLon.X * Math.PI / 180.0 * R * cosLat, latLon.Y * Math.PI / 180.0 * R);
    }

    // Unproject a flat coordinate back to WGS-84.
    // Returns Coordinate(X = longitude, Y = latitude).
    internal static Coordinate Unproject(Coordinate xy, double latRef)
    {
        const double R = 6378137.0;
        double cosLat = Math.Cos(latRef * Math.PI / 180.0);
        return new Coordinate(xy.X / (Math.PI / 180.0 * R * cosLat), xy.Y / (Math.PI / 180.0 * R));
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
            Coordinate flat = Project(new Coordinate(n.Lon, n.Lat), latRef);
            return new double[] { flat.X, flat.Y };
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

            Coordinate latLon = Unproject(new Coordinate(pts[i][0], pts[i][1]), latRef);
            moves.Add((node.Id, node.Lat, node.Lon, latLon.Y, latLon.X));
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
        List<Coordinate> pts = [.. nodeList.Select(n => Project(new Coordinate(n.Lon, n.Lat), latRef))];

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
            Coordinate latLon = Unproject(new Coordinate(newX, newY), latRef);
            moves.Add((nodeList[i].Id, nodeList[i].Lat, nodeList[i].Lon, latLon.Y, latLon.X));
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

    #region GridifyHelpers
    /// <summary>
    ///   Computes the angle (in degrees, 0–360 counter-clockwise from east) of the longest
    ///   edge of a way. Useful for pre-populating the rotation fields in the gridify panel.
    ///   Returns 0.0 when the way cannot be resolved.
    /// </summary>
    /// <param name="wayId">The ID of the way to inspect.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <returns>
    ///   The longest-edge angle in degrees, normalized to [0, 360).
    /// </returns>
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

            Coordinate aFlat = Project(new Coordinate(a.Lon, a.Lat), latRef);
            Coordinate bFlat = Project(new Coordinate(b.Lon, b.Lat), latRef);
            double dx = bFlat.X - aFlat.X;
            double dy = bFlat.Y - aFlat.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);

            if (len > longestLen)
            {
                longestLen = len;
                longestAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            }
        }

        return ((longestAngle % 360.0) + 360.0) % 360.0;
    }

    /// <summary>
    ///   Computes the natural column and row extension angles (the direction each column/row
    ///   itself runs, not the stacking axis) for gridifying a closed quadrilateral way.
    ///   For a quadrilateral, opposite edge pairs are averaged: the pair closer to N-S becomes
    ///   the column extension angle, the pair closer to E-W becomes the row extension angle.
    ///   Falls back to (90°, 180°) for ways that are not exactly 4-sided.
    /// </summary>
    /// <param name="wayId">The ID of the closed way to inspect.</param>
    /// <param name="ways">The current way dictionary from the edit buffer.</param>
    /// <param name="nodes">The current node dictionary from the edit buffer.</param>
    /// <returns>
    ///   <c>ColRotationDeg</c>: angle (°) the column cells extend.
    ///   <c>RowRotationDeg</c>: angle (°) the row cells extend.
    /// </returns>
    public static (double ColRotationDeg, double RowRotationDeg) ComputeGridifyRotations(
        long wayId,
        ImmutableDictionary<long, OsmWay> ways,
        ImmutableDictionary<long, OsmNode> nodes)
    {
        const double defaultCol = 90.0;
        const double defaultRow = 180.0;

        if (!ways.TryGetValue(wayId, out OsmWay? way) || !way.IsClosed)
        {
            return (defaultCol, defaultRow);
        }

        List<long> nodeIds = [.. way.NodeIds.Take(way.NodeIds.Count - 1)];
        if (nodeIds.Count != 4)
        {
            return (defaultCol, defaultRow);
        }

        List<OsmNode> nodeList = [.. nodeIds.Where(nodes.ContainsKey).Select(id => nodes[id])];
        if (nodeList.Count != 4)
        {
            return (defaultCol, defaultRow);
        }

        double latRef = nodeList.Average(n => n.Lat);

        // Angle of each edge, normalised to [0°, 180°) so opposite directions map to the same line.
        double[] a = new double[4];
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4;
            Coordinate pFlat = Project(new Coordinate(nodeList[i].Lon, nodeList[i].Lat), latRef);
            Coordinate qFlat = Project(new Coordinate(nodeList[j].Lon, nodeList[j].Lat), latRef);
            double angle = Math.Atan2(qFlat.Y - pFlat.Y, qFlat.X - pFlat.X) * 180.0 / Math.PI;
            a[i] = ((angle % 180.0) + 180.0) % 180.0;
        }

        // Average opposite edge pairs (edges 0&2 and edges 1&3).
        double avg02 = HalfCircleMean(a[0], a[2]);
        double avg13 = HalfCircleMean(a[1], a[3]);

        // The pair closer to N-S (|sin| ≥ |cos|) is the column extension direction.
        double rad02 = avg02 * Math.PI / 180.0;
        return Math.Abs(Math.Sin(rad02)) >= Math.Abs(Math.Cos(rad02))
            ? (avg02, avg13)
            : (avg13, avg02);
    }

    /// <summary>
    ///   Returns the circular mean of two undirected line angles, each in [0°, 180°),
    ///   handling wrap-around at the 0°/180° boundary.
    /// </summary>
    /// <param name="a">First angle in [0°, 180°).</param>
    /// <param name="b">Second angle in [0°, 180°).</param>
    /// <returns>
    ///   Mean angle in [0°, 180°).
    /// </returns>
    private static double HalfCircleMean(double a, double b)
    {
        double diff = b - a;
        if (diff > 90.0) diff -= 180.0;
        if (diff < -90.0) diff += 180.0;
        return ((a + diff * 0.5) % 180.0 + 180.0) % 180.0;
    }
    #endregion
}
