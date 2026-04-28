using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Handlers.Geom;

/// <inheritdoc />
public sealed class AssembleMultiPolygon(EditBufferStateService editBufferState, GeometryFactory factory)
    : IRequestHandler<AssembleMultiPolygon.Query, QueryResult<Feature>>
{
    /// <summary>
    ///   Assembles a GeoJSON <see cref="Feature"/> with a <see cref="Polygon"/> or
    ///   <see cref="MultiPolygon"/> geometry from the way members of a multipolygon relation.
    ///   Outer and inner way members are chained into rings; when a connecting way is absent
    ///   from the buffer (e.g. outside the current viewport), a straight-line bridge is
    ///   inserted so that a renderable polygon can still be produced. The assembled geometry
    ///   is used for rendering only and is never written back to the edit buffer.
    /// </summary>
    /// <param name="Ref">Typed reference to the relation to assemble.</param>
    /// <returns>
    ///   A success result containing the assembled feature, or a failure result when the
    ///   relation is not a multipolygon, has no outer way members, or produces a degenerate
    ///   geometry.
    /// </returns>
    public record Query(OsmElementRef Ref) : IRequest<QueryResult<Feature>>;

    /// <inheritdoc />
    public Task<QueryResult<Feature>> Handle(Query request, CancellationToken cancellationToken)
    {
        EditBufferState buf = editBufferState.State;

        if (!buf.Relations.TryGetValue(request.Ref.Id, out OsmRelation? relation))
        {
            return Task.FromResult(QueryResult<Feature>.Fail("Relation not found."));
        }

        if (!relation.Tags.TryGetValue("type", out string? relType) || relType != "multipolygon")
        {
            return Task.FromResult(QueryResult<Feature>.Fail("Not a multipolygon relation."));
        }

        List<OsmWay> outerWays = [];
        List<OsmWay> innerWays = [];

        foreach (OsmMember member in relation.Members)
        {
            if (member.Type != OsmElementTypes.Way)
            {
                continue;
            }

            if (!buf.Ways.TryGetValue(member.Ref, out OsmWay? way))
            {
                continue;
            }

            if (member.Role == "outer")
            {
                outerWays.Add(way);
            }
            else if (member.Role == "inner")
            {
                innerWays.Add(way);
            }
        }

        if (outerWays.Count == 0)
        {
            return Task.FromResult(QueryResult<Feature>.Fail("No outer way members found."));
        }

        if (!TryChainIntoRings(outerWays, buf.Nodes, out List<Coordinate[]> outerRings)
            || outerRings.Count == 0)
        {
            return Task.FromResult(QueryResult<Feature>.Fail("Could not chain outer ways into rings."));
        }

        TryChainIntoRings(innerWays, buf.Nodes, out List<Coordinate[]> innerRings);

        Geometry geom;
        try
        {
            if (outerRings.Count == 1)
            {
                LinearRing exterior = factory.CreateLinearRing(outerRings[0]);
                LinearRing[] holes = [.. innerRings.Select(r => factory.CreateLinearRing(r))];
                geom = factory.CreatePolygon(exterior, holes);
            }
            else
            {
                List<Polygon> polygons = [.. outerRings.Select(r =>
                    factory.CreatePolygon(factory.CreateLinearRing(r)))];

                foreach (Coordinate[] innerRing in innerRings)
                {
                    Polygon holeGeom = factory.CreatePolygon(factory.CreateLinearRing(innerRing));
                    Polygon? containing = polygons.FirstOrDefault(p => p.Contains(holeGeom));
                    if (containing is null)
                    {
                        continue;
                    }

                    int idx = polygons.IndexOf(containing);
                    LinearRing[] existingHoles = [.. containing.InteriorRings.Select(r => factory.CreateLinearRing(r.Coordinates)), factory.CreateLinearRing(innerRing)];
                    polygons[idx] = factory.CreatePolygon(factory.CreateLinearRing(containing.ExteriorRing.Coordinates), existingHoles);
                }

                geom = factory.CreateMultiPolygon([.. polygons]);
            }
        }
        catch (ArgumentException)
        {
            return Task.FromResult(QueryResult<Feature>.Fail("Degenerate ring geometry."));
        }

        AttributesTable attrs = new()
        {
            { "id", relation.Id.ToString() },
            { "type", "relation" },
            { "version", relation.Version },
            { "area", "yes" },
            { "fill", OsmWay.WayFillColor(relation.Tags) },
            { "stroke", OsmWay.WayStrokeColor(relation.Tags) }
        };

        foreach ((string key, string value) in relation.Tags)
        {
            attrs.Add("tag:" + key, value);
        }

        return Task.FromResult(QueryResult<Feature>.Pass(new Feature(geom, attrs)));
    }

    private static bool TryChainIntoRings(
        List<OsmWay> wayMembers,
        IReadOnlyDictionary<long, OsmNode> nodes,
        out List<Coordinate[]> rings)
    {
        rings = [];
        if (wayMembers.Count == 0)
        {
            return true;
        }

        List<OsmWay> remaining = [.. wayMembers];

        while (remaining.Count > 0)
        {
            OsmWay startWay = remaining[0];
            remaining.RemoveAt(0);

            long chainStartNodeId = startWay.NodeIds[0];
            List<Coordinate> ringCoords = [];

            if (!AppendWayCoords(startWay, reversed: false, nodes, ringCoords))
            {
                return false;
            }

            long currentEndNodeId = startWay.NodeIds[^1];

            while (currentEndNodeId != chainStartNodeId)
            {
                int exactIdx = remaining.FindIndex(w =>
                    w.NodeIds[0] == currentEndNodeId ||
                    w.NodeIds[^1] == currentEndNodeId);

                OsmWay nextWay;
                bool reversed;

                if (exactIdx >= 0)
                {
                    nextWay = remaining[exactIdx];
                    remaining.RemoveAt(exactIdx);
                    reversed = nextWay.NodeIds[0] != currentEndNodeId;
                }
                else if (remaining.Count > 0)
                {
                    (int idx, bool rev) = FindClosestEndpoint(remaining, ringCoords[^1], nodes);
                    nextWay = remaining[idx];
                    remaining.RemoveAt(idx);
                    reversed = rev;
                }
                else
                {
                    break;
                }

                if (!AppendWayCoords(nextWay, reversed, nodes, ringCoords))
                {
                    return false;
                }

                currentEndNodeId = reversed ? nextWay.NodeIds[0] : nextWay.NodeIds[^1];
            }

            if (!ringCoords[^1].Equals2D(ringCoords[0]))
            {
                ringCoords.Add(new Coordinate(ringCoords[0].X, ringCoords[0].Y));
            }

            if (ringCoords.Count >= 4)
            {
                rings.Add([.. ringCoords]);
            }
        }

        return true;
    }

    private static bool AppendWayCoords(
        OsmWay way,
        bool reversed,
        IReadOnlyDictionary<long, OsmNode> nodes,
        List<Coordinate> ringCoords)
    {
        IEnumerable<long> nodeIds = reversed
            ? ((IEnumerable<long>)way.NodeIds).Reverse()
            : way.NodeIds;

        foreach (long nodeId in nodeIds)
        {
            if (!nodes.TryGetValue(nodeId, out OsmNode? node))
            {
                return false;
            }

            Coordinate coord = new(node.Lon, node.Lat);
            if (ringCoords.Count == 0 || !ringCoords[^1].Equals2D(coord))
            {
                ringCoords.Add(coord);
            }
        }

        return true;
    }

    private static (int Index, bool Reversed) FindClosestEndpoint(
        List<OsmWay> remaining,
        Coordinate currentEnd,
        IReadOnlyDictionary<long, OsmNode> nodes)
    {
        int bestIndex = 0;
        bool bestReversed = false;
        double bestDistSquared = double.MaxValue;

        for (int i = 0; i < remaining.Count; i++)
        {
            OsmWay way = remaining[i];

            if (nodes.TryGetValue(way.NodeIds[0], out OsmNode? first))
            {
                double dx = first.Lon - currentEnd.X;
                double dy = first.Lat - currentEnd.Y;
                double dist = dx * dx + dy * dy;
                if (dist < bestDistSquared)
                {
                    bestDistSquared = dist;
                    bestIndex = i;
                    bestReversed = false;
                }
            }

            if (nodes.TryGetValue(way.NodeIds[^1], out OsmNode? last))
            {
                double dx = last.Lon - currentEnd.X;
                double dy = last.Lat - currentEnd.Y;
                double dist = dx * dx + dy * dy;
                if (dist < bestDistSquared)
                {
                    bestDistSquared = dist;
                    bestIndex = i;
                    bestReversed = true;
                }
            }
        }

        return (bestIndex, bestReversed);
    }
}
