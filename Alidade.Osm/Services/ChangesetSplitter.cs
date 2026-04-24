namespace Alidade.Osm.Services;

/// <summary>
///   Splits a dirty edit buffer into one or more <see cref="OsmChange"/> objects that
///   each fit within the OSM API element limit. The algorithm proceeds in six steps:
///   <list type="number">
///     <item>Collect all dirty elements (created, modified, or deleted).</item>
///     <item>
///       Build a dependency graph: ways depend on their member nodes; relations depend
///       on all their members.
///     </item>
///     <item>Assign each element a spatial cluster via its zoom-14 slippy tile centroid.</item>
///     <item>
///       Merge clusters whose elements are co-required by a relation using a union-find
///       structure, so that relations and all their members land in the same changeset.
///     </item>
///     <item>
///       Pack merged clusters into changesets, respecting
///       <c>maxElementsPerChangeset</c> (default 10,000).
///     </item>
///     <item>
///       Within each changeset, produce elements in topological order:
///       nodes → ways → relations.
///     </item>
///   </list>
/// </summary>
public static class ChangesetSplitter
{
    /// <summary>
    ///   Splits the dirty elements in <paramref name="buffer"/> into as many
    ///   <see cref="OsmChange"/> objects as needed so that each contains at most
    ///   <paramref name="maxElementsPerChangeset"/> elements.
    /// </summary>
    /// <param name="buffer">The current edit buffer state.</param>
    /// <param name="maxElementsPerChangeset">
    ///   Maximum number of elements per changeset. Defaults to 10,000 (the OSM API limit).
    /// </param>
    /// <returns>
    ///   An ordered list of <see cref="OsmChange"/> objects ready for sequential upload,
    ///   or an empty list when the buffer has no dirty elements.
    /// </returns>
    public static IReadOnlyList<OsmChange> Split(
        EditBufferState buffer,
        int maxElementsPerChangeset = 10_000)
    {
        // Collect dirty elements
        List<OsmNode> dirtyNodes = [.. buffer.Nodes.Values.Where(n => buffer.EditStates.TryGetValue(n.Ref, out EditState s) && s != EditState.Fetched)];
        List<OsmWay> dirtyWays = [.. buffer.Ways.Values.Where(w => buffer.EditStates.TryGetValue(w.Ref, out EditState s) && s != EditState.Fetched)];
        List<OsmRelation> dirtyRelations = [.. buffer.Relations.Values.Where(r => buffer.EditStates.TryGetValue(r.Ref, out EditState s) && s != EditState.Fetched)];

        if (dirtyNodes.Count == 0 && dirtyWays.Count == 0 && dirtyRelations.Count == 0)
        {
            return [];
        }

        // Assign slippy tile cluster IDs (zoom 14)
        const int clusterZoom = 14;
        Dictionary<OsmElementRef, (int X, int Y)> elementCluster = [];

        foreach (OsmNode n in dirtyNodes)
        {
            elementCluster[n.Ref] = LatLonToTile(n.Lat, n.Lon, clusterZoom);
        }

        foreach (OsmWay w in dirtyWays)
        {
            elementCluster[w.Ref] = CentroidTileForWay(w, buffer.Nodes, clusterZoom);
        }

        foreach (OsmRelation r in dirtyRelations)
        {
            elementCluster[r.Ref] = CentroidTileForRelation(r, buffer.Nodes, buffer.Ways, clusterZoom);
        }

        // Merge clusters for relation co-membership
        UnionFind<(int X, int Y)> uf = new(elementCluster.Values.Distinct());

        foreach (OsmRelation r in dirtyRelations)
        {
            (int X, int Y) relTile = elementCluster[r.Ref];
            foreach (OsmMember m in r.Members)
            {
                OsmElementRef mRef = new(m.Type, m.Ref);
                if (elementCluster.TryGetValue(mRef, out (int X, int Y) mTile))
                {
                    uf.Union(relTile, mTile);
                }
            }
        }

        // Map each element to its root cluster.
        Dictionary<(int X, int Y), List<OsmElementRef>> clusterGroups = [];
        foreach (KeyValuePair<OsmElementRef, (int X, int Y)> pair in elementCluster)
        {
            (int X, int Y) root = uf.Find(pair.Value);
            if (!clusterGroups.ContainsKey(root))
            {
                clusterGroups[root] = [];
            }

            clusterGroups[root].Add(pair.Key);
        }

        // Pack clusters into changesets
        List<List<OsmElementRef>> changeSets = [];
        List<OsmElementRef> current = [];

        foreach (List<OsmElementRef> group in clusterGroups.Values)
        {
            // If adding this cluster would overflow and we already have elements, flush first.
            if (current.Count + group.Count > maxElementsPerChangeset && current.Count > 0)
            {
                changeSets.Add(current);
                current = [];
            }

            current.AddRange(group);

            if (current.Count >= maxElementsPerChangeset)
            {
                changeSets.Add(current);
                current = [];
            }
        }

        if (current.Count > 0)
        {
            changeSets.Add(current);
        }

        // Build OsmChange per changeset (topological order)
        return [.. changeSets.Select(refs => BuildChange(refs, buffer))];
    }

    #region Helpers
    private static OsmChange BuildChange(List<OsmElementRef> refs, EditBufferState buffer)
    {
        List<OsmNode> createdNodes = [];
        List<OsmNode> modifiedNodes = [];
        List<long> deletedNodeIds = [];

        List<OsmWay> createdWays = [];
        List<OsmWay> modifiedWays = [];
        List<long> deletedWayIds = [];

        List<OsmRelation> createdRelations = [];
        List<OsmRelation> modifiedRelations = [];
        List<long> deletedRelationIds = [];

        foreach (OsmElementRef r in refs)
        {
            EditState state = buffer.EditStates.GetValueOrDefault(r);
            switch (r.Type)
            {
                case OsmElementTypes.Node:
                    if (!buffer.Nodes.TryGetValue(r.Id, out OsmNode? node))
                    {
                        break;
                    }

                    if (state == EditState.Created)
                    {
                        createdNodes.Add(node);
                    }
                    else if (state == EditState.Modified)
                    {
                        modifiedNodes.Add(node);
                    }
                    else if (state == EditState.Deleted)
                    {
                        deletedNodeIds.Add(r.Id);
                    }

                    break;

                case OsmElementTypes.Way:
                    if (!buffer.Ways.TryGetValue(r.Id, out OsmWay? way))
                    {
                        break;
                    }

                    if (state == EditState.Created)
                    {
                        createdWays.Add(way);
                    }
                    else if (state == EditState.Modified)
                    {
                        modifiedWays.Add(way);
                    }
                    else if (state == EditState.Deleted)
                    {
                        deletedWayIds.Add(r.Id);
                    }

                    break;

                case OsmElementTypes.Relation:
                    if (!buffer.Relations.TryGetValue(r.Id, out OsmRelation? rel))
                    {
                        break;
                    }

                    if (state == EditState.Created)
                    {
                        createdRelations.Add(rel);
                    }
                    else if (state == EditState.Modified)
                    {
                        modifiedRelations.Add(rel);
                    }
                    else if (state == EditState.Deleted)
                    {
                        deletedRelationIds.Add(r.Id);
                    }

                    break;
            }
        }

        return new OsmChange(
            createdNodes, createdWays, createdRelations,
            modifiedNodes, modifiedWays, modifiedRelations,
            deletedNodeIds, deletedWayIds, deletedRelationIds);
    }

    private static (int X, int Y) LatLonToTile(double lat, double lon, int zoom)
    {
        int n = 1 << zoom;
        int x = (int)((lon + 180.0) / 360.0 * n);
        double latRad = lat * Math.PI / 180.0;
        int y = (int)((1.0 - Math.Log(Math.Tan(latRad) + 1.0 / Math.Cos(latRad)) / Math.PI) / 2.0 * n);
        return (Math.Clamp(x, 0, n - 1), Math.Clamp(y, 0, n - 1));
    }

    private static (int X, int Y) CentroidTileForWay(
        OsmWay way,
        ImmutableDictionary<long, OsmNode> nodes,
        int zoom)
    {
        List<OsmNode> pts = [.. way.NodeIds
            .Select(id => nodes.GetValueOrDefault(id))
            .Where(n => n is not null)
            .Cast<OsmNode>()];

        if (pts.Count == 0)
        {
            return (0, 0);
        }

        return LatLonToTile(pts.Average(n => n.Lat), pts.Average(n => n.Lon), zoom);
    }

    private static (int X, int Y) CentroidTileForRelation(
        OsmRelation rel,
        ImmutableDictionary<long, OsmNode> nodes,
        ImmutableDictionary<long, OsmWay> ways,
        int zoom)
    {
        List<double> lats = [];
        List<double> lons = [];

        foreach (OsmMember m in rel.Members)
        {
            if (m.Type == OsmElementTypes.Node && nodes.TryGetValue(m.Ref, out OsmNode? n))
            {
                lats.Add(n.Lat);
                lons.Add(n.Lon);
            }
            else if (m.Type == OsmElementTypes.Way && ways.TryGetValue(m.Ref, out OsmWay? w))
            {
                foreach (long nid in w.NodeIds)
                {
                    if (nodes.TryGetValue(nid, out OsmNode? wn))
                    {
                        lats.Add(wn.Lat);
                        lons.Add(wn.Lon);
                    }
                }
            }
        }

        if (lats.Count == 0)
        {
            return (0, 0);
        }

        return LatLonToTile(lats.Average(), lons.Average(), zoom);
    }

    #endregion

    /// <summary>
    ///   Simple union-find over arbitrary keys
    /// </summary>
    /// <typeparam name="T"></typeparam>
    private sealed class UnionFind<T> where T : notnull
    {
        private readonly Dictionary<T, T> _parent = [];

        /// <summary>
        ///   Initializes the union-find with one singleton set per item.
        /// </summary>
        /// <param name="items">The initial set of elements.</param>
        public UnionFind(IEnumerable<T> items)
        {
            foreach (T i in items)
            {
                _parent[i] = i;
            }
        }

        /// <summary>
        ///   Returns the canonical representative of the set containing <paramref name="x"/>,
        ///   applying path compression.
        /// </summary>
        /// <param name="x">The element to find.</param>
        /// <returns>The root representative of the set.</returns>
        public T Find(T x)
        {
            if (!_parent.TryGetValue(x, out T? p))
            {
                _parent[x] = x;
                return x;
            }

            if (!p.Equals(x))
            {
                _parent[x] = Find(p);
            }

            return _parent[x];
        }

        /// <summary>
        ///   Merges the sets containing <paramref name="a"/> and <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The first element.</param>
        /// <param name="b">The second element.</param>
        public void Union(T a, T b)
        {
            T ra = Find(a);
            T rb = Find(b);

            if (!ra.Equals(rb))
            {
                _parent[ra] = rb;
            }
        }
    }
}
