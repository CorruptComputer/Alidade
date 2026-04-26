using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Osm.Services;

/// <inheritdoc />
internal sealed class OsmCacheService(GeometryFactory geomFactory) : IOsmCacheService
{
    // TODO: Replace the O(n) linear scans in GetGeometryFromBbox with three
    //       NTS Quadtree<long> spatial indexes (one per element type) if profiling
    //       shows the scan becoming a bottleneck during long panning sessions.
    //       Quadtree supports incremental inserts; STRtree is bulk-load only.
    private Geometry? _cachedArea;
    private readonly Dictionary<long, OsmNode> _nodes = [];
    private readonly Dictionary<long, OsmWay> _ways = [];
    private readonly Dictionary<long, OsmRelation> _relations = [];
    private readonly Dictionary<long, Feature> _nodeFeatures = [];
    private readonly Dictionary<long, Feature> _wayFeatures = [];

    private readonly GeometryFactory _geomFactory = geomFactory;

    /// <inheritdoc />
    public List<CacheBounds> GetGeometryMissBboxes(CacheBounds request)
    {
        if (_cachedArea is null)
        {
            return [request];
        }

        Geometry requestGeom = BoundsToGeometry(request);
        Geometry diff = requestGeom.Difference(_cachedArea);

        if (diff.IsEmpty)
        {
            return [];
        }

        return DecomposeToBboxes(diff);
    }

    /// <inheritdoc />
    public void AddToCache(CacheBounds bounds, OsmCacheData data)
    {
        Geometry newGeom = BoundsToGeometry(bounds);
        _cachedArea = _cachedArea is null
            ? newGeom
            : _cachedArea.Union(newGeom);

        foreach (OsmNode n in data.Nodes)
        {
            _nodes[n.Id] = n;
            _nodeFeatures[n.Id] = n.ToFeature(_geomFactory);
        }

        foreach (OsmWay w in data.Ways)
        {
            _ways[w.Id] = w;
            Feature? f = w.ToFeature(_nodes, _geomFactory);
            if (f is not null)
            {
                _wayFeatures[w.Id] = f;
            }
        }

        foreach (OsmRelation r in data.Relations)
        {
            _relations[r.Id] = r;
        }
    }

    /// <inheritdoc />
    public OsmCacheData GetGeometryFromBbox(CacheBounds bounds)
    {
        Geometry requestGeom = BoundsToGeometry(bounds);

        if (_cachedArea is null || !_cachedArea.Covers(requestGeom))
        {
            throw new InvalidOperationException(
                $"Requested bbox ({bounds}) includes area outside the OSM cache.");
        }

        List<OsmNode> nodesInBbox = [.. _nodes.Values
            .Where(n => n.Lat <= bounds.North
                        && n.Lon <= bounds.East
                        && n.Lat >= bounds.South
                        && n.Lon >= bounds.West)];

        HashSet<long> nodeIdsInBbox = nodesInBbox.Select(n => n.Id).ToHashSet();

        List<OsmWay> ways = [.. _ways.Values.Where(w => w.NodeIds.Any(id => nodeIdsInBbox.Contains(id)))];

        // Include out-of-bbox nodes referenced by included ways so that rendering
        // doesn't break for ways that cross the viewport boundary — the OSM API
        // always returns full way geometry including nodes outside the requested bbox.
        HashSet<long> allNodeIds = [..nodeIdsInBbox];
        List<OsmNode> allNodes = [..nodesInBbox];

        foreach (OsmWay way in ways)
        {
            foreach (long nodeId in way.NodeIds)
            {
                if (allNodeIds.Add(nodeId) && _nodes.TryGetValue(nodeId, out OsmNode? extraNode))
                {
                    allNodes.Add(extraNode);
                }
            }
        }

        HashSet<long> wayIds = [.. ways.Select(w => w.Id)];

        List<OsmRelation> relations = [.. _relations.Values
            .Where(r => r.Members.Any(m =>
                (m.Type == OsmElementTypes.Node && allNodeIds.Contains(m.Ref))
                || (m.Type == OsmElementTypes.Way && wayIds.Contains(m.Ref)))
            )];

        return new OsmCacheData(allNodes, ways, relations);
    }

    /// <inheritdoc />
    public Feature? GetCachedNodeFeature(long id) => _nodeFeatures.GetValueOrDefault(id);

    /// <inheritdoc />
    public Feature? GetCachedWayFeature(long id) => _wayFeatures.GetValueOrDefault(id);

    /// <inheritdoc />
    public void Clear()
    {
        _cachedArea = null;
        _nodes.Clear();
        _nodeFeatures.Clear();
        _ways.Clear();
        _wayFeatures.Clear();
        _relations.Clear();
    }

    private Geometry BoundsToGeometry(CacheBounds b)
        => _geomFactory.ToGeometry(new Envelope(b.West, b.East, b.South, b.North));

    private static CacheBounds EnvelopeToCacheBounds(Envelope env)
        => new(env.MinX, env.MinY, env.MaxX, env.MaxY);

    /// <summary>
    ///   Decomposes an orthogonal difference polygon (or multipolygon) into a minimal
    ///   set of axis-aligned bboxes using horizontal strip decomposition. Each consecutive
    ///   pair of unique latitude values in the geometry defines a horizontal band; the
    ///   intersection of the diff with that band yields one or more bboxes for that strip.
    ///   An L-shaped diff (typical single-pan case) produces exactly two bboxes.
    /// </summary>
    private List<CacheBounds> DecomposeToBboxes(Geometry diff)
    {
        double[] ys = [.. diff.Coordinates
            .Select(c => c.Y)
            .Distinct()
            .OrderBy(y => y)];

        List<CacheBounds> result = [];
        Envelope diffEnv = diff.EnvelopeInternal;

        for (int i = 0; i < ys.Length - 1; i++)
        {
            double yLow  = ys[i];
            double yHigh = ys[i + 1];

            // Horizontal slab spanning the band, extended past diff's X extent so the
            // intersection clips cleanly against the diff's actual X boundary.
            Envelope slabEnv = new(diffEnv.MinX - 1.0, diffEnv.MaxX + 1.0, yLow, yHigh);
            Geometry slab = _geomFactory.ToGeometry(slabEnv);
            Geometry band = diff.Intersection(slab);

            if (band.IsEmpty)
            {
                continue;
            }

            if (band is GeometryCollection gc)
            {
                for (int j = 0; j < gc.NumGeometries; j++)
                {
                    Geometry part = gc.GetGeometryN(j);
                    if (!part.IsEmpty)
                    {
                        result.Add(EnvelopeToCacheBounds(part.EnvelopeInternal));
                    }
                }
            }
            else
            {
                result.Add(EnvelopeToCacheBounds(band.EnvelopeInternal));
            }
        }

        return result;
    }
}
