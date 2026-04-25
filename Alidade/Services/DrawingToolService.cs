using Alidade.Handlers.Selection;
using Alidade.Osm.Handlers.Editing;

namespace Alidade.Services;

/// <summary>
///   Routes map interaction events (click, hover, drag) to handlers based on the currently
///   active tool. Called by Questy notification handlers; inject and use via those handlers.
/// </summary>
public class DrawingToolService(
    IMediator mediator,
    ToolStateService toolState,
    EditBufferStateService editBuffer,
    SelectionStateService selectionState,
    ILogger<DrawingToolService> log)
{
    /// <summary>
    ///   Routes a map click to the appropriate tool action.
    /// </summary>
    /// <param name="e">The click event data.</param>
    public async Task HandleMapClickAsync(MapClickEvent e)
    {
        switch (toolState.State.Active)
        {
            case ActiveTools.Select:
                await HandleSelectClickAsync(e);
                break;

            case ActiveTools.DrawNode:
                if (e.ElementId?.StartsWith("way/") == true
                    && long.TryParse(e.ElementId[4..], out long drawHitWayId))
                {
                    await SplitWayAtPointAsync(drawHitWayId, e.Lat, e.Lon);
                }
                else
                {
                    await mediator.Send(new CreateNode.Command(
                        e.Lat, e.Lon, new Dictionary<string, string>()));
                }
                await mediator.Send(new Handlers.Tool.SetActiveTool.Command(ActiveTools.Select));
                break;

            case ActiveTools.DrawWay:
            case ActiveTools.DrawArea:
                await HandleDrawWayClickAsync(e, toolState.State.Active == ActiveTools.DrawArea);
                break;
        }
    }

    /// <summary>
    ///   Routes a map double-click to the appropriate tool action.
    /// </summary>
    /// <param name="e">The click event data.</param>
    public async Task HandleMapDblClickAsync(MapClickEvent e)
    {
        if (toolState.State.Active != ActiveTools.Select)
        {
            return;
        }

        if (e.ElementId?.StartsWith("way/") == true
            && long.TryParse(e.ElementId[4..], out long wayId))
        {
            await SplitWayAtPointAsync(wayId, e.Lat, e.Lon);
        }
    }

    /// <summary>
    ///   Routes a hover change to the selection state.
    /// </summary>
    /// <param name="elementId">The feature ID under the cursor, or <c>null</c> when leaving.</param>
    public async Task HandleHoverAsync(string? elementId)
    {
        if (elementId is null)
        {
            await mediator.Send(new SetHovered.Command(null));
            return;
        }

        string[] parts = elementId.Split('/');
        if (parts.Length != 2 || !long.TryParse(parts[1], out long id))
        {
            return;
        }

        OsmElementTypes? type = parts[0] switch
        {
            "node"     => OsmElementTypes.Node,
            "way"      => OsmElementTypes.Way,
            "relation" => OsmElementTypes.Relation,
            _          => null
        };

        if (type is null)
        {
            return;
        }

        await mediator.Send(new SetHovered.Command(new OsmElementRef(type.Value, id)));
    }

    /// <summary>
    ///   Routes a node drag-end event to a move, merge, or way-snap action.
    /// </summary>
    /// <param name="e">The drag event data.</param>
    public async Task HandleNodeDragEndAsync(NodeDragEvent e)
    {
        if (!long.TryParse(e.ElementId.Replace("node/", string.Empty), out long nodeId))
        {
            log.LogWarning("NodeDragEnd: could not parse node ID from '{ElementId}'", e.ElementId);
            return;
        }

        EditBufferState buf = editBuffer.State;
        if (!buf.Nodes.TryGetValue(nodeId, out OsmNode? node))
        {
            log.LogWarning("NodeDragEnd: node {NodeId} not found in edit buffer", nodeId);
            return;
        }

        if (e.SnapTargetId is not null
            && e.SnapTargetId.StartsWith("node/")
            && long.TryParse(e.SnapTargetId[5..], out long snapTargetId)
            && snapTargetId != nodeId
            && buf.Nodes.ContainsKey(snapTargetId))
        {
            await mediator.Send(new MergeNodes.Command(nodeId, snapTargetId));
            await mediator.Send(new Select.Command(
                new OsmElementRef(OsmElementTypes.Node, snapTargetId), false));
            return;
        }

        if (e.WaySnapTargetId is not null
            && e.WaySnapTargetId.StartsWith("way/")
            && long.TryParse(e.WaySnapTargetId[4..], out long waySnapId)
            && buf.Ways.TryGetValue(waySnapId, out OsmWay? snapWay))
        {
            (double snapLat, double snapLon, int insertIndex) = ResolveWaySnapPosition(
                e.Lat, e.Lon, snapWay, waySnapId, e.WaySnapSegmentNodeA, e.WaySnapSegmentNodeB, buf);

            await mediator.Send(new MoveNode.Command(
                nodeId, node.Lat, node.Lon, snapLat, snapLon));

            await mediator.Send(new AddNodeToWay.Command(
                waySnapId, nodeId, insertIndex));

            return;
        }

        await mediator.Send(new MoveNode.Command(
            nodeId, node.Lat, node.Lon, e.Lat, e.Lon));
    }

    private async Task HandleSelectClickAsync(MapClickEvent e)
    {
        if (e.ElementId is null)
        {
            await mediator.Send(new Select.Command(null, false));
            return;
        }

        string[] parts = e.ElementId.Split('/');
        if (parts.Length != 2 || !long.TryParse(parts[1], out long id))
        {
            return;
        }

        OsmElementTypes? type = parts[0] switch
        {
            "node" => OsmElementTypes.Node,
            "way" => OsmElementTypes.Way,
            "relation" => OsmElementTypes.Relation,
            _ => null
        };

        if (type is null)
        {
            return;
        }

        OsmElementRef targetRef = new(type.Value, id);

        if (!e.AddToSelection && type == OsmElementTypes.Way)
        {
            EditBufferState buf = editBuffer.State;
            if (buf.Ways.TryGetValue(id, out OsmWay? clickedWay))
            {
                (long NodeA, long NodeB)? seg = FindNearestSegmentNodes(e.Lat, e.Lon, clickedWay, buf.Nodes);
                if (seg.HasValue)
                {
                    List<long> sharing = FindWaysSharingSegment(seg.Value.NodeA, seg.Value.NodeB, buf.Ways, buf.EditStates);
                    if (sharing.Count > 1)
                    {
                        OsmElementRef? current = selectionState.State.SingleSelected;
                        if (current?.Type == OsmElementTypes.Way)
                        {
                            int currentIdx = sharing.IndexOf(current.Id);
                            if (currentIdx >= 0)
                            {
                                long nextId = sharing[(currentIdx + 1) % sharing.Count];
                                targetRef = new OsmElementRef(OsmElementTypes.Way, nextId);
                            }
                        }
                    }
                }
            }
        }

        await mediator.Send(new Select.Command(targetRef, e.AddToSelection));
    }

    private async Task HandleDrawWayClickAsync(MapClickEvent e, bool isArea)
    {
        ToolState ts = toolState.State;
        long? snapNodeId = ts.SnapTargetNodeId;
        if (!snapNodeId.HasValue
            && e.ElementId?.StartsWith("node/") == true
            && long.TryParse(e.ElementId[5..], out long hitNodeId))
        {
            snapNodeId = hitNodeId;
        }

        double lat = e.Lat;
        double lon = e.Lon;
        long? existingNodeId = null;

        if (snapNodeId.HasValue
            && editBuffer.State.Nodes.TryGetValue(snapNodeId.Value, out OsmNode? snapNode))
        {
            lat = snapNode.Lat;
            lon = snapNode.Lon;
            existingNodeId = snapNodeId.Value;
        }

        int count = ts.WayInProgress.Count;

        bool isTerminal = e.ElementId == "draw-preview-terminal";

        bool clickedFirst = count >= 1
            && ((existingNodeId is not null && existingNodeId == ts.WayInProgress[0].NodeId)
                || (isArea && isTerminal));

        bool clickedLast = count >= 1
            && ((existingNodeId is not null && existingNodeId == ts.WayInProgress[count - 1].NodeId)
                || (!isArea && isTerminal));

        if (isArea)
        {
            if (clickedFirst)
            {
                await mediator.Send(new Handlers.Tool.EndDrawing.Command());
                return;
            }
        }
        else
        {
            if (clickedLast)
            {
                await mediator.Send(new Handlers.Tool.EndDrawing.Command());
                return;
            }
        }

        if (e.ElementId == "draw-preview-line")
        {
            (double projLat, double projLon, int insertIndex)? proj = ProjectOntoWayInProgress(lat, lon, ts.WayInProgress);
            if (proj.HasValue)
            {
                long nodeId = editBuffer.State.NextNegativeId;
                await mediator.Send(new CreateNode.Command(
                    proj.Value.projLat, proj.Value.projLon, new Dictionary<string, string>()));
                await mediator.Send(new Handlers.Tool.AddNodeToWay.Command(
                    proj.Value.insertIndex, proj.Value.projLat, proj.Value.projLon, nodeId));
            }

            return;
        }

        if (existingNodeId is null
            && e.ElementId?.StartsWith("way/") == true
            && long.TryParse(e.ElementId[4..], out long hitWayId))
        {
            existingNodeId = await SplitWayAtPointAsync(hitWayId, lat, lon);
            if (existingNodeId.HasValue
                && editBuffer.State.Nodes.TryGetValue(existingNodeId.Value, out OsmNode? splitNode))
            {
                lat = splitNode.Lat;
                lon = splitNode.Lon;
            }
        }

        await mediator.Send(new Handlers.Tool.AppendWayPoint.Command(lat, lon, existingNodeId));
    }

    private async Task<long?> SplitWayAtPointAsync(long wayId, double lat, double lon)
    {
        EditBufferState buf = editBuffer.State;
        if (!buf.Ways.TryGetValue(wayId, out OsmWay? way))
        {
            return null;
        }

        IReadOnlyDictionary<long, OsmNode> nodes = buf.Nodes;
        IReadOnlyList<long> nodeIds = way.NodeIds;
        int bestIdx = -1;
        double bestDist = double.MaxValue;
        double bestLat = lat, bestLon = lon;

        for (int i = 0; i < nodeIds.Count - 1; i++)
        {
            if (!nodes.TryGetValue(nodeIds[i], out OsmNode? a)
                || !nodes.TryGetValue(nodeIds[i + 1], out OsmNode? b))
            {
                continue;
            }

            (double projLat, double projLon) = ProjectOntoSegment(lat, lon, a.Lat, a.Lon, b.Lat, b.Lon);
            double d = CoordDist(lat, lon, projLat, projLon);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i + 1;
                bestLat = projLat;
                bestLon = projLon;
            }
        }

        if (bestIdx < 0)
        {
            return null;
        }

        long newNodeId = buf.NextNegativeId;
        await mediator.Send(new CreateNode.Command(
            bestLat, bestLon, new Dictionary<string, string>()));
        await mediator.Send(new AddNodeToWay.Command(wayId, newNodeId, bestIdx));
        return newNodeId;
    }

    /// <summary>
    ///   Commits the current in-progress way or area using the current tool state.
    /// </summary>
    /// <param name="isArea">Whether to commit as a closed area.</param>
    public Task CommitCurrentDrawingAsync(bool isArea)
        => CommitWayAsync(toolState.State, isArea);

    private async Task CommitWayAsync(ToolState ts, bool isArea)
    {
        List<long> nodeIds = [];
        long nextId = editBuffer.State.NextNegativeId;

        foreach ((double ptLat, double ptLon, long? existingId) in ts.WayInProgress)
        {
            if (existingId.HasValue)
            {
                nodeIds.Add(existingId.Value);
            }
            else
            {
                nodeIds.Add(nextId);
                await mediator.Send(new CreateNode.Command(
                    ptLat, ptLon, new Dictionary<string, string>()));
                nextId--;
            }
        }

        if (isArea)
        {
            nodeIds.Add(nodeIds[0]);
        }

        Dictionary<string, string> tags = isArea
            ? new Dictionary<string, string> { ["area"] = "yes" }
            : [];

        await mediator.Send(new CreateWay.Command(nodeIds, tags));
        await mediator.Send(new Handlers.Tool.ClearWayInProgress.Command());
        await mediator.Send(new Handlers.Tool.SetActiveTool.Command(ActiveTools.Select));
    }

    private static (long NodeA, long NodeB)? FindNearestSegmentNodes(
        double lat, double lon, OsmWay way, IReadOnlyDictionary<long, OsmNode> nodes)
    {
        IReadOnlyList<long> nodeIds = way.NodeIds;
        int bestIdx = -1;
        double bestDist = double.MaxValue;

        for (int i = 0; i < nodeIds.Count - 1; i++)
        {
            if (!nodes.TryGetValue(nodeIds[i], out OsmNode? a)
                || !nodes.TryGetValue(nodeIds[i + 1], out OsmNode? b))
            {
                continue;
            }

            (double projLat, double projLon) = ProjectOntoSegment(lat, lon, a.Lat, a.Lon, b.Lat, b.Lon);
            double d = CoordDist(lat, lon, projLat, projLon);
            if (d < bestDist)
            {
                bestDist = d;
                bestIdx = i;
            }
        }

        if (bestIdx < 0)
        {
            return null;
        }

        return (nodeIds[bestIdx], nodeIds[bestIdx + 1]);
    }

    private static List<long> FindWaysSharingSegment(
        long nodeA, long nodeB,
        IReadOnlyDictionary<long, OsmWay> ways,
        IReadOnlyDictionary<OsmElementRef, EditState> editStates)
    {
        List<long> result = [];

        foreach ((long wayId, OsmWay way) in ways)
        {
            if (editStates.GetValueOrDefault(way.Ref) == EditState.Deleted)
            {
                continue;
            }

            IReadOnlyList<long> nodeIds = way.NodeIds;
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                if ((nodeIds[i] == nodeA && nodeIds[i + 1] == nodeB)
                    || (nodeIds[i] == nodeB && nodeIds[i + 1] == nodeA))
                {
                    result.Add(wayId);
                    break;
                }
            }
        }

        result.Sort();
        return result;
    }

    private static (double Lat, double Lon, int InsertIndex) ResolveWaySnapPosition(
        double lat, double lon,
        OsmWay snapWay,
        long wayId,
        string? segNodeAStr,
        string? segNodeBStr,
        EditBufferState buf)
    {
        IReadOnlyList<long> nodeIds = snapWay.NodeIds;
        IReadOnlyDictionary<long, OsmNode> nodes = buf.Nodes;

        if (segNodeAStr is not null
            && segNodeBStr is not null
            && long.TryParse(segNodeAStr, out long segA)
            && long.TryParse(segNodeBStr, out long segB))
        {
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                if (nodeIds[i] == segA
                    && nodeIds[i + 1] == segB
                    && nodes.TryGetValue(segA, out OsmNode? a)
                    && nodes.TryGetValue(segB, out OsmNode? b))
                {
                    (double projLat, double projLon) = ProjectOntoSegment(lat, lon, a.Lat, a.Lon, b.Lat, b.Lon);
                    return (projLat, projLon, i + 1);
                }
            }
        }

        (double Lat, double Lon, long WayId, int InsertIndex)? fallback =
            FindNearestWaySegment(lat, lon, -1, wayId, buf);

        return fallback.HasValue
            ? (fallback.Value.Lat, fallback.Value.Lon, fallback.Value.InsertIndex)
            : (lat, lon, nodeIds.Count);
    }

    private static (double Lat, double Lon, long WayId, int InsertIndex)? FindNearestWaySegment(
        double lat, double lon, long excludeNodeId, long? onlyWayId, EditBufferState buf)
    {
        IReadOnlyDictionary<long, OsmNode> nodes = buf.Nodes;
        double bestDist = double.MaxValue;
        (double Lat, double Lon, long WayId, int InsertIndex)? best = null;

        IEnumerable<KeyValuePair<long, OsmWay>> candidates = onlyWayId.HasValue
            ? buf.Ways.Where(kv => kv.Key == onlyWayId.Value)
            : buf.Ways.Where(kv => !kv.Value.NodeIds.Contains(excludeNodeId));

        foreach ((long wayId, OsmWay way) in candidates)
        {
            IReadOnlyList<long> nodeIds = way.NodeIds;
            for (int i = 0; i < nodeIds.Count - 1; i++)
            {
                // Skip segments that already include the dragged node — snapping to an
                // adjacent segment would insert a duplicate next to itself (degenerate spike).
                if (nodeIds[i] == excludeNodeId || nodeIds[i + 1] == excludeNodeId)
                {
                    continue;
                }

                if (!nodes.TryGetValue(nodeIds[i], out OsmNode? a)
                    || !nodes.TryGetValue(nodeIds[i + 1], out OsmNode? b))
                {
                    continue;
                }

                (double projLat, double projLon) = ProjectOntoSegment(lat, lon, a.Lat, a.Lon, b.Lat, b.Lon);
                double d = CoordDist(lat, lon, projLat, projLon);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = (projLat, projLon, wayId, i + 1);
                }
            }
        }

        return best;
    }

    private static (double projLat, double projLon, int insertIndex)? ProjectOntoWayInProgress(
        double lat, double lon, ImmutableList<(double Lat, double Lon, long? NodeId)> pts)
    {
        if (pts.Count < 2)
        {
            return null;
        }

        double bestDist = double.MaxValue;
        double bestLat = lat, bestLon = lon;
        int bestInsert = 1;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            (double projLat, double projLon) = ProjectOntoSegment(lat, lon, pts[i].Lat, pts[i].Lon, pts[i + 1].Lat, pts[i + 1].Lon);
            double d = CoordDist(lat, lon, projLat, projLon);
            if (d < bestDist)
            {
                bestDist = d;
                bestLat = projLat;
                bestLon = projLon;
                bestInsert = i + 1;
            }
        }

        return (bestLat, bestLon, bestInsert);
    }

    private static (double Lat, double Lon) ProjectOntoSegment(
        double lat, double lon,
        double aLat, double aLon, double bLat, double bLon)
    {
        double dx = bLon - aLon;
        double dy = bLat - aLat;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-20)
        {
            return (aLat, aLon);
        }

        double t = Math.Clamp(((lon - aLon) * dx + (lat - aLat) * dy) / lenSq, 0.0, 1.0);
        return (aLat + t * dy, aLon + t * dx);
    }

    private static double CoordDist(double lat1, double lon1, double lat2, double lon2)
    {
        double dlat = lat1 - lat2;
        double dlon = lon1 - lon2;
        return Math.Sqrt(dlat * dlat + dlon * dlon);
    }
}
