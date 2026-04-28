using Alidade.Osm.Models.Tools.Circularize;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Tools.Circularize;

/// <inheritdoc />
public sealed class CircularizeWay(EditBufferStateService editBufferState)
    : IRequestHandler<CircularizeWay.Query, QueryResult<CircularizeResult>>
{
    /// <summary>
    ///   Computes node moves that fit a closed way to its best-fit circle, adding new nodes
    ///   between existing ones when the arc segments would otherwise exceed the target spacing.
    ///   Key nodes — those shared with another way or carrying tags — define arc boundaries and
    ///   are snapped to the circle at their existing angle; fill nodes in each arc are
    ///   distributed at uniform angular spacing.
    ///   Returns a <see cref="CircularizeResult"/> containing the moves to commit.
    /// </summary>
    /// <param name="WayRef">The element reference of the closed way to circularize.</param>
    /// <param name="VertexCount">
    ///   Target number of vertices for the output circle. When <see langword="null"/>, the count
    ///   is derived from the radius using iD's MAX_SEGMENT_LENGTH formula (4 m segments, clamped
    ///   to 12–32 vertices).
    /// </param>
    public record Query(OsmElementRef WayRef, int? VertexCount = null) : IRequest<QueryResult<CircularizeResult>>;

    // Target arc segment length in meters, matching iD's MAX_SEGMENT_LENGTH.
    private const double MaxSegmentLength = 4.0;

    // Minimum vertex count, matching iD's MIN_VERTICES.
    private const int MinVertices = 12;

    // Maximum vertex count, matching iD's MAX_VERTICES.
    private const int MaxVertices = 32;

    /// <inheritdoc />
    public Task<QueryResult<CircularizeResult>> Handle(Query request, CancellationToken cancellationToken)
    {
        EditBufferState buf = editBufferState.State;
        CircularizeResult result = Compute(request.WayRef.Id, buf.Ways, buf.Nodes, request.VertexCount);
        return result.Moves.Count == 0
            ? Task.FromResult(QueryResult<CircularizeResult>.Fail("The way cannot be circularized."))
            : Task.FromResult(QueryResult<CircularizeResult>.Pass(result));
    }

    private static CircularizeResult Compute(long wayId, ImmutableDictionary<long, OsmWay> ways, ImmutableDictionary<long, OsmNode> nodes, int? vertexCount = null)
    {
        if (!ways.TryGetValue(wayId, out OsmWay? way) || !way.IsClosed)
        {
            return CircularizeResult.Empty;
        }

        List<long> nodeIds = [.. way.NodeIds.Take(way.NodeIds.Count - 1)];
        List<OsmNode> nodeList = [.. nodeIds.Where(nodes.ContainsKey).Select(id => nodes[id])];
        if (nodeList.Count < 3)
        {
            return CircularizeResult.Empty;
        }

        double latRef = nodeList.Average(n => n.Lat);
        List<Coordinate> pts = [.. nodeList.Select(n => GeometryService.Project(new Coordinate(n.Lon, n.Lat), latRef))];

        double cx = pts.Average(p => p.X);
        double cy = pts.Average(p => p.Y);
        double r = pts.Average(p => Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy)));
        if (r < 1e-6)
        {
            return CircularizeResult.Empty;
        }

        int targetCount = vertexCount ?? GetTargetVertexCount(r);
        double maxAngle = 2.0 * Math.PI / (targetCount - 1);

        // Winding: positive signed area (Y-up flat projection) = CCW = sign 1; negative = CW = sign -1.
        double signedArea = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            Coordinate a = pts[i];
            Coordinate b = pts[(i + 1) % pts.Count];
            signedArea += a.X * b.Y - b.X * a.Y;
        }
        double sign = signedArea >= 0 ? 1.0 : -1.0;

        // Key nodes: shared with another way in the edit buffer, or carrying tags.
        HashSet<long> sharedNodeIds = FindSharedNodeIds(ways, wayId, nodeList);
        List<int> keyIndices = [.. Enumerable.Range(0, nodeList.Count)
            .Where(i => sharedNodeIds.Contains(nodeList[i].Id) || nodeList[i].Tags.Count > 0)];

        if (keyIndices.Count == 0)
        {
            keyIndices.Add(0);
        }
        if (keyIndices.Count == 1)
        {
            keyIndices.Add((keyIndices[0] + nodeList.Count / 2) % nodeList.Count);
        }

        List<(long, Coordinate?, Coordinate)> moves = [];
        List<CircularizeNodeRef> finalWayNodeList = [];
        int newNodeCount = 0;

        for (int i = 0; i < keyIndices.Count; i++)
        {
            int startIdx = keyIndices[i];
            int endIdx = keyIndices[(i + 1) % keyIndices.Count];
            int indexRange = (endIdx - startIdx + nodeList.Count) % nodeList.Count;
            if (indexRange == 0)
            {
                indexRange = nodeList.Count;
            }

            double startAngle = Math.Atan2(pts[startIdx].Y - cy, pts[startIdx].X - cx);
            double endAngle = Math.Atan2(pts[endIdx].Y - cy, pts[endIdx].X - cx);
            double totalAngle = endAngle - startAngle;

            // Ensure totalAngle goes in the polygon's winding direction. In Y-up coordinates,
            // CCW arcs have positive totalAngle and CW arcs have negative. Flip when the raw
            // difference crosses ±π in the wrong direction (opposite of iD's Y-down check).
            if (totalAngle * sign < 0)
            {
                totalAngle = sign * (2.0 * Math.PI - Math.Abs(totalAngle));
            }

            // Find the minimum number of additional nodes so every arc segment ≤ maxAngle.
            int numberNewNodes = 0;
            double eachAngle;
            do
            {
                eachAngle = totalAngle / (indexRange + numberNewNodes);
                if (Math.Abs(eachAngle) <= maxAngle)
                {
                    break;
                }
                numberNewNodes++;
            } while (numberNewNodes <= MaxVertices);

            // Snap the start key node onto the circle (preserve angle, force distance = r).
            OsmNode startNode = nodeList[startIdx];
            Coordinate snappedStart = GeometryService.Unproject(
                new Coordinate(cx + r * Math.Cos(startAngle), cy + r * Math.Sin(startAngle)), latRef);
            moves.Add((startNode.Id, new Coordinate(startNode.Lon, startNode.Lat), snappedStart));
            finalWayNodeList.Add(CircularizeNodeRef.Existing(startNode.Id));

            // Redistribute existing in-between nodes at uniform angular spacing.
            for (int j = 1; j < indexRange; j++)
            {
                double angle = startAngle + j * eachAngle;
                Coordinate newPos = GeometryService.Unproject(
                    new Coordinate(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)), latRef);
                OsmNode mid = nodeList[(startIdx + j) % nodeList.Count];
                moves.Add((mid.Id, new Coordinate(mid.Lon, mid.Lat), newPos));
                finalWayNodeList.Add(CircularizeNodeRef.Existing(mid.Id));
            }

            // Insert new fill nodes to close any remaining angular gap.
            for (int j = 0; j < numberNewNodes; j++)
            {
                double angle = startAngle + (indexRange + j) * eachAngle;
                Coordinate newPos = GeometryService.Unproject(
                    new Coordinate(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)), latRef);
                moves.Add((0L, null, newPos));
                finalWayNodeList.Add(CircularizeNodeRef.New(newNodeCount++));
            }
        }

        // Close the way by repeating the first node reference.
        if (finalWayNodeList.Count > 0)
        {
            finalWayNodeList.Add(finalWayNodeList[0]);
        }

        // Only return FinalWayNodeList when new nodes were inserted; a plain moves-only result
        // is sufficient otherwise and avoids unnecessarily rewriting the way's node list.
        return new CircularizeResult(moves, newNodeCount > 0 ? finalWayNodeList : []);
    }

    /// <summary>
    ///   Returns the target vertex count for a circle of the given radius in approximate meters,
    ///   using iD's formula clamped to [<see cref="MinVertices"/>, <see cref="MaxVertices"/>].
    /// </summary>
    /// <param name="radiusMeters">The radius in approximate meters (from the flat-Earth projection).</param>
    /// <returns>The target number of vertices.</returns>
    private static int GetTargetVertexCount(double radiusMeters)
        => Math.Clamp((int)Math.Round(radiusMeters * Math.PI / MaxSegmentLength) * 2, MinVertices, MaxVertices);

    private static HashSet<long> FindSharedNodeIds(
        ImmutableDictionary<long, OsmWay> ways, long wayId, List<OsmNode> nodeList)
    {
        HashSet<long> wayNodeIds = [.. nodeList.Select(n => n.Id)];
        HashSet<long> shared = [];

        foreach (OsmWay other in ways.Values)
        {
            if (other.Id == wayId)
            {
                continue;
            }
            foreach (long nodeId in other.NodeIds)
            {
                if (wayNodeIds.Contains(nodeId))
                {
                    shared.Add(nodeId);
                }
            }
        }

        return shared;
    }
}
