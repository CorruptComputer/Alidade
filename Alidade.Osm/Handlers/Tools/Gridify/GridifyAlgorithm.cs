using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Gridify;

/// <summary>
///   Helper methods for the gridify tool's geometry computations.
/// </summary>
public static class GridifyAlgorithm
{
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
            Coordinate pFlat = GeometryService.Project(new Coordinate(nodeList[i].Lon, nodeList[i].Lat), latRef);
            Coordinate qFlat = GeometryService.Project(new Coordinate(nodeList[j].Lon, nodeList[j].Lat), latRef);
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
}
