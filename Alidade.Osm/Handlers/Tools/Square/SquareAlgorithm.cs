namespace Alidade.Osm.Handlers.Tools.Square;

/// <summary>
///   Shared bisector-motion algorithm helpers used by the Square query handlers.
///   All geometry is performed in a local flat-Earth projection; callers are responsible
///   for projecting coordinates before calling and unprojecting results afterward.
/// </summary>
internal static class SquareAlgorithm
{
    internal const double DegThreshold = 13.0;
    internal const double Epsilon = 1e-4;
    internal const int MaxIterations = 1000;

    internal static readonly double LowerThreshold = Math.Cos((90.0 - DegThreshold) * Math.PI / 180.0);
    internal static readonly double UpperThreshold = Math.Cos(DegThreshold * Math.PI / 180.0);

    /// <summary>
    ///   Computes the bisector motion for a single node toward the nearest 90° angle.
    /// </summary>
    /// <param name="origin">The node position being corrected.</param>
    /// <param name="a">The previous neighbour in the ring.</param>
    /// <param name="b">The next neighbour in the ring.</param>
    /// <param name="isClosed">Whether the ring is closed (endpoints wrap around).</param>
    /// <param name="i">The index of this node within the simplified corner set.</param>
    /// <param name="count">Total count of nodes in the simplified corner set.</param>
    /// <returns>
    ///   A two-element array <c>[dx, dy]</c> representing the motion to apply.
    ///   Returns <c>[0, 0]</c> when the node should not move.
    /// </returns>
    internal static double[] CalcMotion(double[] origin, double[] a, double[] b,
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

        if (Math.Abs(dotp) < LowerThreshold)
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

    /// <summary>
    ///   Returns the normalised dot product of the vectors from <paramref name="o"/> to
    ///   <paramref name="a"/> and from <paramref name="o"/> to <paramref name="b"/>.
    /// </summary>
    /// <param name="a">First point.</param>
    /// <param name="b">Second point.</param>
    /// <param name="o">Origin point.</param>
    /// <returns>Normalised dot product in [−1, 1], or 0 when a vector is degenerate.</returns>
    internal static double NormalizedDot(double[] a, double[] b, double[] o)
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

    /// <summary>
    ///   Sums the non-right-angle contribution of each corner node in the simplified set.
    ///   Lower scores indicate better orthogonality; 0 is fully squared.
    /// </summary>
    /// <param name="pts">All node positions in the ring (flat space).</param>
    /// <param name="simplified">Indices of corner nodes to score.</param>
    /// <param name="isClosed">Whether the ring is closed.</param>
    /// <returns>Aggregate non-orthogonality score (lower is better).</returns>
    internal static double CalcScore(List<double[]> pts, List<int> simplified, bool isClosed)
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
}
