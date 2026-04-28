using NetTopologySuite.Geometries;

namespace Alidade.Osm.Services;

/// <summary>
///   Geometric operations: segment math and coordinate projection helpers.
///   Works in a local flat-Earth projection (Web Mercator approximation) to preserve
///   distance relationships.
/// </summary>
public static class GeometryService
{
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
}
