using Alidade.Core.Models.CQRS.Response;
using Alidade.Map.Handlers;
using Alidade.Osm.Handlers.Api.Editing;
using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Models;
using Alidade.Osm.Models.Editing;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Services;

/// <summary>
///   Subscribes to <see cref="EditBufferStateService"/>, <see cref="SelectionStateService"/>,
///   and <see cref="MapStateService"/> changes, pushes GeoJSON <see cref="FeatureCollection"/>s
///   to MapLibre via Questy map commands, triggers OSM data fetches when the viewport moves
///   to zoom 16+, and provides the <see cref="ReplaceState"/> and <see cref="MergeFetchedData"/>
///   operations used by undo/redo and data fetch flows.
/// </summary>
public class EditBufferService : IDisposable
{
    private const double MinFetchZoom = 16.0;
    private const double EvictionMultiplier = 2.0;
    private const double FetchPaddingMultiplier = 1.2;
    private readonly IMediator _mediator;
    private readonly NsiService _nsi;
    private readonly IndexedDBService _storage;
    private readonly EditBufferStateService _editState;
    private readonly MapStateService _mapState;
    private readonly SelectionStateService _selectionState;
    private readonly IOsmCacheService _osmCache;
    private readonly ValidationService? _validation;
    private readonly GeometryFactory _geomFactory;
    private readonly ILogger<EditBufferService> _log;

    // Draft persistence
    private CancellationTokenSource? _saveCts;
    private bool _hadDirtyState;

    // Selection push debouncing and delta tracking
    private int _selectionPushSeq;
    private ImmutableHashSet<OsmElementRef> _lastPushedSelected = [];
    private ImmutableHashSet<long> _lastPushedWayIds = [];
    private bool _selectedNeedsRepush;

    // Fetch cancellation
    private CancellationTokenSource _fetchCts = new();

    /// <summary>
    ///   Initializes the service and wires up state-change subscriptions.
    /// </summary>
    public EditBufferService(
        IMediator mediator,
        NsiService nsi,
        IndexedDBService storage,
        GeometryFactory geomFactory,
        EditBufferStateService editState,
        MapStateService mapState,
        SelectionStateService selectionState,
        IOsmCacheService osmCache,
        ILogger<EditBufferService> log,
        ValidationService? validation = null)
    {
        _mediator = mediator;
        _nsi = nsi;
        _storage = storage;
        _geomFactory = geomFactory;
        _editState = editState;
        _mapState = mapState;
        _selectionState = selectionState;
        _osmCache = osmCache;
        _validation = validation;
        _log = log;

        _editState.StateChanged += OnEditBufferChanged;
        _mapState.StateChanged += OnMapStateChanged;
        _selectionState.StateChanged += OnSelectionChanged;
    }

    /// <summary>
    ///   The current edit buffer state.
    /// </summary>
    public EditBufferState State => _editState.State;

    private void OnEditBufferChanged(object? sender, EventArgs e)
    {
        _selectedNeedsRepush = true;
        RequestSelectionPush();
        _validation?.ScheduleValidation(_editState.State);
        ScheduleSave(_editState.State);
    }

    private void OnMapStateChanged(object? sender, EventArgs e)
    {
        MapBounds? bounds = _mapState.State.CurrentBounds;

        if (bounds is not null)
        {
            CancellationTokenSource oldCts = _fetchCts;
            _fetchCts = new CancellationTokenSource();
            _ = Task.Run(() =>
            {
                oldCts.Cancel();
                oldCts.Dispose();
            });

            EvictOutOfViewportData(bounds);

            _ = _mediator.Publish(new FetchBboxRequested.Notification(bounds), _fetchCts.Token);

            if (bounds.Zoom >= MinFetchZoom)
            {
                double centerLat = (bounds.South + bounds.North) / 2.0;
                double centerLon = (bounds.West + bounds.East) / 2.0;
                _nsi.UpdateForLocation(centerLat, centerLon);
            }
            else
            {
                _nsi.LoadRegions([]);
            }
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        RequestSelectionPush();
    }

    private void RequestSelectionPush()
    {
        int seq = ++_selectionPushSeq;

        _ = _mediator.Publish(new SelectionPushRequested.Notification(seq));
    }

    internal async Task RunSelectionPushAsync(int seq)
    {
        await Task.Yield();
        if (_selectionPushSeq != seq)
        {
            return;
        }

        EditBufferState bufSnap = _editState.State;
        SelectionState selSnap = _selectionState.State;

        bool needsRepush = _selectedNeedsRepush;
        bool refChanged = !ReferenceEquals(selSnap.Selected, _lastPushedSelected);
        bool includeSelected = needsRepush || refChanged;
        _selectedNeedsRepush = false;
        _lastPushedSelected = selSnap.Selected;

        HashSet<long> modifiedNodeIds = [.. bufSnap.EditStates
            .Where(kv => kv.Key.Type == OsmElementTypes.Node && kv.Value != EditState.Fetched)
            .Select(kv => kv.Key.Id)];

        if (includeSelected)
        {
            ImmutableHashSet<long> newWayIds = selSnap.Selected
                .Where(r => r.Type == OsmElementTypes.Way)
                .Select(r => r.Id)
                .ToImmutableHashSet();

            bool waySelectionChanged = !newWayIds.SetEquals(_lastPushedWayIds);
            _lastPushedWayIds = newWayIds;

            if (needsRepush || waySelectionChanged)
            {
                await RunPushGeoJsonAsync(bufSnap, modifiedNodeIds);
            }
        }

        await PushSelectionAsync(selSnap, bufSnap, includeSelected, modifiedNodeIds);
    }

    #region State mutation

    /// <summary>
    ///   Directly replaces the entire edit buffer state. Used by undo/redo.
    /// </summary>
    public void ReplaceState(EditBufferState state) => _editState.SetState(state);

    /// <summary>
    ///   Merges freshly fetched OSM data into the buffer, skipping elements with local edits.
    /// </summary>
    public void MergeFetchedData(IReadOnlyList<OsmNode> nodes, IReadOnlyList<OsmWay> ways, IReadOnlyList<OsmRelation> relations)
    {
        EditBufferState state = _editState.State;
        ImmutableDictionary<long, OsmNode> nodeDict = state.Nodes;
        ImmutableDictionary<long, OsmWay> wayDict = state.Ways;
        ImmutableDictionary<long, OsmRelation> relDict = state.Relations;
        ImmutableDictionary<OsmElementRef, EditState> editStates = state.EditStates;

        List<KeyValuePair<long, OsmNode>> nodeUpdates = [];
        List<KeyValuePair<OsmElementRef, EditState>> nodeEditStateUpdates = [];
        foreach (OsmNode node in nodes)
        {
            if (!editStates.TryGetValue(node.Ref, out EditState es) || es == EditState.Fetched)
            {
                nodeUpdates.Add(new KeyValuePair<long, OsmNode>(node.Id, node));
                nodeEditStateUpdates.Add(new KeyValuePair<OsmElementRef, EditState>(node.Ref, EditState.Fetched));
            }
        }

        List<KeyValuePair<long, OsmWay>> wayUpdates = [];
        List<KeyValuePair<OsmElementRef, EditState>> wayEditStateUpdates = [];
        foreach (OsmWay way in ways)
        {
            if (!editStates.TryGetValue(way.Ref, out EditState es) || es == EditState.Fetched)
            {
                wayUpdates.Add(new KeyValuePair<long, OsmWay>(way.Id, way));
                wayEditStateUpdates.Add(new KeyValuePair<OsmElementRef, EditState>(way.Ref, EditState.Fetched));
            }
        }

        List<KeyValuePair<long, OsmRelation>> relUpdates = [];
        List<KeyValuePair<OsmElementRef, EditState>> relEditStateUpdates = [];
        foreach (OsmRelation rel in relations)
        {
            if (!editStates.TryGetValue(rel.Ref, out EditState es) || es == EditState.Fetched)
            {
                relUpdates.Add(new KeyValuePair<long, OsmRelation>(rel.Id, rel));
                relEditStateUpdates.Add(new KeyValuePair<OsmElementRef, EditState>(rel.Ref, EditState.Fetched));
            }
        }

        nodeDict = nodeDict.SetItems(nodeUpdates);
        wayDict = wayDict.SetItems(wayUpdates);
        relDict = relDict.SetItems(relUpdates);
        editStates = editStates.SetItems([.. nodeEditStateUpdates, .. wayEditStateUpdates, .. relEditStateUpdates]);

        _editState.SetState(state with { Nodes = nodeDict, Ways = wayDict, Relations = relDict, EditStates = editStates });
    }

    /// <summary>
    ///   Resets the edit buffer to its initial empty state, cancels any in-flight data
    ///   fetches so stale endpoint data cannot re-populate the buffer, and immediately
    ///   re-fetches from the current viewport so the new endpoint's data loads without
    ///   requiring a pan.
    /// </summary>
    public void Clear()
    {
        _fetchCts.Cancel();
        _fetchCts.Dispose();
        _fetchCts = new CancellationTokenSource();
        _osmCache.Clear();
        _editState.SetState(new EditBufferState());

        MapBounds? bounds = _mapState.State.CurrentBounds;
        if (bounds is not null)
        {
            _ = _mediator.Publish(new FetchBboxRequested.Notification(bounds), _fetchCts.Token);
        }
    }

    #endregion

    #region GeoJSON push

    internal async Task RunPushGeoJsonAsync(EditBufferState state, HashSet<long> modifiedNodeIds)
    {
        IReadOnlyDictionary<long, OsmNode> liveNodes = state.Nodes
            .Where(kv => state.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Deleted)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Count how many distinct live ways each node participates in so that
        // junction nodes (shared by 2+ ways) can be styled differently.
        Dictionary<long, int> nodeWayCount = [];
        foreach (OsmWay w in state.Ways.Values)
        {
            if (state.EditStates.GetValueOrDefault(w.Ref) == EditState.Deleted)
            {
                continue;
            }

            HashSet<long> seen = [];
            foreach (long nodeId in w.NodeIds)
            {
                if (seen.Add(nodeId))
                {
                    nodeWayCount[nodeId] = nodeWayCount.GetValueOrDefault(nodeId) + 1;
                }
            }
        }

        // Nodes belonging to a selected way are always shown so their vertex
        // handles are visible and hoverable while the way is selected.
        SelectionState sel = _selectionState.State;
        HashSet<long> selectedWayNodeIds = [];
        foreach (OsmElementRef elemRef in sel.Selected)
        {
            if (elemRef.Type == OsmElementTypes.Way
                && state.Ways.TryGetValue(elemRef.Id, out OsmWay? sw))
            {
                foreach (long nid in sw.NodeIds)
                {
                    selectedWayNodeIds.Add(nid);
                }
            }
        }

        FeatureCollection nodes = [];
        foreach (OsmNode n in liveNodes.Values)
        {
            Feature f = NodeFeatureForPush(n, state);
            bool hasTags = n.Tags.Count > 0;
            bool isJunction = nodeWayCount.GetValueOrDefault(n.Id) >= 2;
            (string fill, string stroke) = (hasTags, isJunction) switch
            {
                (true, true) => ("#4cc", "#2aa"), // tagged junction: medium cyan
                (true, false) => ("#9ef", "#3bb"), // tagged standalone: light cyan
                (false, true) => ("#ccc", "#888"), // untagged junction: gray
                _ => ("#fff", "#555") // untagged standalone: white
            };

            bool isWayNode = nodeWayCount.ContainsKey(n.Id);
            bool forceShow = selectedWayNodeIds.Contains(n.Id);

            f.Attributes["fill"] = fill;
            f.Attributes["stroke"] = stroke;
            f.Attributes["show"] = (hasTags || isJunction || !isWayNode || forceShow)
                ? "yes"
                : "no";

            nodes.Add(f);
        }

        FeatureCollection ways = [];
        foreach (OsmWay w in state.Ways.Values)
        {
            if (state.EditStates.GetValueOrDefault(w.Ref) == EditState.Deleted)
            {
                continue;
            }

            Feature? f = WayFeatureForPush(w, state, liveNodes, modifiedNodeIds);
            if (f is not null)
            {
                ways.Add(f);
            }
        }

        await SetSourceAsync(MapSourceNames.Nodes, nodes);
        await SetSourceAsync(MapSourceNames.Ways, ways);
    }

    private async Task PushSelectionAsync(SelectionState sel, EditBufferState buf, bool includeSelected, HashSet<long> modifiedNodeIds)
    {
        IReadOnlyDictionary<long, OsmNode> liveNodes = buf.Nodes
            .Where(kv => buf.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Deleted)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (includeSelected)
        {
            FeatureCollection selectedFeatures = [];
            FeatureCollection vertexFeatures = [];

            foreach (OsmElementRef elemRef in sel.Selected)
            {
                switch (elemRef.Type)
                {
                    case OsmElementTypes.Node when buf.Nodes.TryGetValue(elemRef.Id, out OsmNode? n):
                        selectedFeatures.Add(NodeFeatureForPush(n, buf));
                        break;

                    case OsmElementTypes.Way when buf.Ways.TryGetValue(elemRef.Id, out OsmWay? w)
                        && buf.EditStates.GetValueOrDefault(w.Ref) != EditState.Deleted:
                        {
                            Feature? wayFeature = WayFeatureForPush(w, buf, liveNodes, modifiedNodeIds);
                            if (wayFeature is not null)
                            {
                                selectedFeatures.Add(wayFeature);
                            }

                            HashSet<long> seen = [];
                            foreach (long nodeId in w.NodeIds)
                            {
                                if (!seen.Add(nodeId))
                                {
                                    continue;
                                }

                                if (!buf.Nodes.TryGetValue(nodeId, out OsmNode? vn))
                                {
                                    continue;
                                }

                                if (buf.EditStates.GetValueOrDefault(vn.Ref) == EditState.Deleted)
                                {
                                    continue;
                                }

                                Feature vf = vn.ToFeature(_geomFactory);
                                vf.Attributes.Add("vertex", "yes");
                                vertexFeatures.Add(vf);
                            }
                            break;
                        }

                    case OsmElementTypes.Relation when buf.Relations.TryGetValue(elemRef.Id, out OsmRelation? r):
                        {
                            foreach (OsmMember member in r.Members.Where(m => m.Type == OsmElementTypes.Way))
                            {
                                if (buf.Ways.TryGetValue(member.Ref, out OsmWay? mw))
                                {
                                    Feature? mf = WayFeatureForPush(mw, buf, liveNodes, modifiedNodeIds);
                                    if (mf is not null)
                                    {
                                        selectedFeatures.Add(mf);
                                    }
                                }
                            }
                            break;
                        }
                }
            }

            await SetSourceAsync(MapSourceNames.Selected, selectedFeatures);
            await SetSourceAsync(MapSourceNames.Vertices, vertexFeatures);
        }

        FeatureCollection hoverFeatures = [];
        // Use the live hover state rather than the snapshot to avoid restoring a hover
        // that was cleared by a concurrent push while PushGeoJsonAsync was awaited.
        OsmElementRef? currentHovered = _selectionState.State.Hovered;
        if (currentHovered is not null)
        {
            switch (currentHovered.Type)
            {
                case OsmElementTypes.Node when buf.Nodes.TryGetValue(currentHovered.Id, out OsmNode? hn):
                    hoverFeatures.Add(NodeFeatureForPush(hn, buf));
                    break;
                case OsmElementTypes.Way when buf.Ways.TryGetValue(currentHovered.Id, out OsmWay? hw):
                    {
                        Feature? hf = WayFeatureForPush(hw, buf, liveNodes, modifiedNodeIds);
                        if (hf is not null)
                        {
                            hoverFeatures.Add(hf);
                        }

                        break;
                    }
                default:
                    _log.LogWarning("[SelectionPush] osm-hover: hovered={Hovered} not found in buf", currentHovered);
                    break;
            }
        }

        await SetSourceAsync(MapSourceNames.Hover, hoverFeatures);
    }

    private Task SetSourceAsync(string sourceId, FeatureCollection featureCollection)
        => _mediator.Send(new SetSourceData.Command(sourceId, featureCollection));

    // Only use the pre-cached Feature when the element is unmodified. Modified nodes/ways
    // have stale geometry in the cache (the position from the original API fetch), so they
    // must always be recomputed from current edit buffer state.
    private Feature NodeFeatureForPush(OsmNode n, EditBufferState state)
    {
        bool useCache = state.EditStates.TryGetValue(n.Ref, out EditState es) && es == EditState.Fetched;
        return (useCache ? _osmCache.GetCachedNodeFeature(n.Id) : null) ?? n.ToFeature(_geomFactory);
    }

    private Feature? WayFeatureForPush(OsmWay w, EditBufferState state, IReadOnlyDictionary<long, OsmNode> liveNodes, HashSet<long> modifiedNodeIds)
    {
        bool useCache = state.EditStates.TryGetValue(w.Ref, out EditState wes) && wes == EditState.Fetched
            && !w.NodeIds.Any(modifiedNodeIds.Contains);
        return (useCache ? _osmCache.GetCachedWayFeature(w.Id) : null) ?? w.ToFeature(liveNodes, _geomFactory);
    }

    private async Task PushNotesAsync(CacheBounds bounds)
    {
        IReadOnlyList<OsmNote> notes = _osmCache.GetNotesFromBbox(bounds);
        FeatureCollection fc = new();
        foreach (OsmNote note in notes)
        {
            AttributesTable attrs = new();
            attrs.Add("id", note.Id);
            attrs.Add("status", note.Status);
            OsmNoteComment? firstComment = note.Comments.FirstOrDefault();
            if (firstComment is not null)
            {
                attrs.Add("text", firstComment.Text);
            }

            fc.Add(new Feature(_geomFactory.CreatePoint(new Coordinate(note.Lon, note.Lat)), attrs));
        }

        await SetSourceAsync(MapSourceNames.Notes, fc);
    }

    #endregion

    #region Data fetch

    private static CacheBounds ExpandBbox(CacheBounds bounds)
    {
        double latPad = (bounds.North - bounds.South) * (FetchPaddingMultiplier - 1.0) / 2.0;
        double lonPad = (bounds.East - bounds.West) * (FetchPaddingMultiplier - 1.0) / 2.0;

        return new CacheBounds(
            bounds.West - lonPad, bounds.South - latPad,
            bounds.East + lonPad, bounds.North + latPad);
    }

    private void EvictOutOfViewportData(MapBounds bounds)
    {
        EditBufferState state = _editState.State;

        double latSpan = bounds.North - bounds.South;
        double lonSpan = bounds.East - bounds.West;
        double latPad = latSpan * (EvictionMultiplier - 1.0) / 2.0;
        double lonPad = lonSpan * (EvictionMultiplier - 1.0) / 2.0;
        double keepSouth = bounds.South - latPad;
        double keepNorth = bounds.North + latPad;
        double keepWest = bounds.West - lonPad;
        double keepEast = bounds.East + lonPad;

        // Pass 1: evict Fetched ways with no node inside keep-bounds.
        List<long> wayIdsToEvict = [];
        foreach (KeyValuePair<long, OsmWay> kv in state.Ways)
        {
            if (state.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Fetched)
            {
                continue;
            }

            bool hasNodeInBounds = false;
            foreach (long nodeId in kv.Value.NodeIds)
            {
                if (state.Nodes.TryGetValue(nodeId, out OsmNode? n)
                    && n.Lat <= keepNorth
                    && n.Lon <= keepEast
                    && n.Lat >= keepSouth
                    && n.Lon >= keepWest)
                {
                    hasNodeInBounds = true;
                    break;
                }
            }

            if (!hasNodeInBounds)
            {
                wayIdsToEvict.Add(kv.Key);
            }
        }

        // Node IDs still claimed by surviving ways must be retained.
        HashSet<long> survivingWayNodeIds = [];
        foreach (KeyValuePair<long, OsmWay> kv in state.Ways)
        {
            if (!wayIdsToEvict.Contains(kv.Key))
            {
                foreach (long nodeId in kv.Value.NodeIds)
                {
                    survivingWayNodeIds.Add(nodeId);
                }
            }
        }

        // Pass 2: evict Fetched nodes outside keep-bounds not claimed by a surviving way.
        List<long> nodeIdsToEvict = [];
        foreach (KeyValuePair<long, OsmNode> kv in state.Nodes)
        {
            if (state.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Fetched)
            {
                continue;
            }

            OsmNode node = kv.Value;
            bool outsideBounds = node.Lat < keepSouth || node.Lat > keepNorth
                              || node.Lon < keepWest  || node.Lon > keepEast;

            if (outsideBounds && !survivingWayNodeIds.Contains(kv.Key))
            {
                nodeIdsToEvict.Add(kv.Key);
            }
        }

        // Pass 3: evict Fetched relations with no surviving member remaining in the buffer.
        HashSet<long> evictedNodeIds = [.. nodeIdsToEvict];
        HashSet<long> evictedWayIds  = [.. wayIdsToEvict];

        List<long> relIdsToEvict = [];
        foreach (KeyValuePair<long, OsmRelation> kv in state.Relations)
        {
            if (state.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Fetched)
            {
                continue;
            }

            bool hasSurvivingMember = false;
            foreach (OsmMember member in kv.Value.Members)
            {
                switch (member.Type)
                {
                    case OsmElementTypes.Node when state.Nodes.ContainsKey(member.Ref)
                        && !evictedNodeIds.Contains(member.Ref):
                        hasSurvivingMember = true;
                        break;

                    case OsmElementTypes.Way when state.Ways.ContainsKey(member.Ref)
                        && !evictedWayIds.Contains(member.Ref):
                        hasSurvivingMember = true;
                        break;

                    case OsmElementTypes.Relation when state.Relations.ContainsKey(member.Ref):
                        hasSurvivingMember = true;
                        break;
                }

                if (hasSurvivingMember)
                {
                    break;
                }
            }

            if (!hasSurvivingMember)
            {
                relIdsToEvict.Add(kv.Key);
            }
        }

        if (wayIdsToEvict.Count == 0 && nodeIdsToEvict.Count == 0 && relIdsToEvict.Count == 0)
        {
            return;
        }

        List<OsmElementRef> editStateKeysToEvict = [];
        foreach (long id in nodeIdsToEvict)
        {
            editStateKeysToEvict.Add(new OsmElementRef(OsmElementTypes.Node, id));
        }
        foreach (long id in wayIdsToEvict)
        {
            editStateKeysToEvict.Add(new OsmElementRef(OsmElementTypes.Way, id));
        }
        foreach (long id in relIdsToEvict)
        {
            editStateKeysToEvict.Add(new OsmElementRef(OsmElementTypes.Relation, id));
        }

        _editState.SetState(state with
        {
            Nodes      = state.Nodes.RemoveRange(nodeIdsToEvict),
            Ways       = state.Ways.RemoveRange(wayIdsToEvict),
            Relations  = state.Relations.RemoveRange(relIdsToEvict),
            EditStates = state.EditStates.RemoveRange(editStateKeysToEvict)
        });
    }

    internal async Task RunFetchBboxAsync(MapBounds bounds, IMediator mediator, CancellationToken ct)
    {
        if (bounds.Zoom < MinFetchZoom)
        {
            return;
        }

        try
        {
            CacheBounds cacheBounds = new(bounds.West, bounds.South, bounds.East, bounds.North);
            List<CacheBounds> missBboxes = _osmCache.GetGeometryMissBboxes(cacheBounds);

            List<Task<(CacheBounds Expanded, FetchBboxResult? Geo, OsmNote[] Notes)>> fetchTasks = [.. missBboxes
                .Select(async miss =>
                {
                    CacheBounds expanded = ExpandBbox(miss);
                    Bbox bbox = new(
                        new NetTopologySuite.Geometries.Coordinate(expanded.West, expanded.North),
                        new NetTopologySuite.Geometries.Coordinate(expanded.East, expanded.South));
                    Task<QueryResult<FetchBboxResult>> geoTask = mediator.Send(new FetchBbox.Query(bbox), ct);
                    Task<QueryResult<OsmNote[]>> notesTask = mediator.Send(new FetchNotes.Query(bbox), ct);

                    await Task.WhenAll(geoTask, notesTask);
                    FetchBboxResult? geo = geoTask.Result;
                    OsmNote[]? fetchedNotes = notesTask.Result;
                    return (expanded, geo, fetchedNotes ?? []);
                })];

            (CacheBounds Expanded, FetchBboxResult? Geo, OsmNote[] Notes)[] fetched = await Task.WhenAll(fetchTasks);
            bool allSucceeded = true;

            foreach ((CacheBounds expanded, FetchBboxResult? geo, OsmNote[] notes) in fetched)
            {
                if (geo is not null)
                {
                    _osmCache.AddToCache(expanded, new OsmCacheData(
                        (IReadOnlyList<OsmNode>)geo.Nodes,
                        (IReadOnlyList<OsmWay>)geo.Ways,
                        (IReadOnlyList<OsmRelation>)geo.Relations,
                        notes));
                }
                else
                {
                    allSucceeded = false;
                }
            }

            // Merge from cache when the full viewport is available.
            // allSucceeded remains true when missBboxes is empty (full cache hit),
            // covering the case where the user navigates back to a previously visited area.
            if (allSucceeded)
            {
                OsmCacheData viewportData = _osmCache.GetGeometryFromBbox(cacheBounds);
                MergeFetchedData(viewportData.Nodes, viewportData.Ways, viewportData.Relations);
                await PushNotesAsync(cacheBounds);
            }
        }
        catch (OperationCanceledException)
        {
            // Fetch cancelled by Clear() during an endpoint switch.
        }
    }

    #endregion

    #region Draft persistence

    /// <summary>
    ///   Checks IndexedDB for a previously saved draft and returns it if dirty elements exist.
    /// </summary>
    public async Task<EditBufferDraft?> TryLoadDraftAsync()
    {
        EditBufferDraft? draft = await _storage.GetDraftAsync();
        return draft is { DirtyCount: > 0 } ? draft : null;
    }

    /// <summary>
    ///   Deletes the saved draft from IndexedDB.
    /// </summary>
    public async Task DeleteDraftAsync()
    {
        await _storage.DeleteDraftAsync();
    }

    private void ScheduleSave(EditBufferState state)
    {
        _saveCts?.Cancel();

        if (state.IsDirty)
        {
            _hadDirtyState = true;
            _saveCts = new CancellationTokenSource();
            _ = _mediator.Publish(new SaveDraftRequested.Notification(state), _saveCts.Token);
        }
        else if (_hadDirtyState)
        {
            _saveCts = null;
            _ = _mediator.Publish(new DeleteDraftRequested.Notification());
        }
    }

    internal async Task RunSaveDraftDebounced(EditBufferState state, CancellationToken token)
    {
        try
        {
            await Task.Delay(1000, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SaveDraftAsync(state);
    }

    private async Task SaveDraftAsync(EditBufferState state)
    {
        HashSet<long> dirtyNodeIds = [];
        HashSet<long> dirtyWayIds = [];
        HashSet<long> dirtyRelationIds = [];

        foreach ((OsmElementRef r, EditState es) in state.EditStates)
        {
            if (es == EditState.Fetched) continue;
            switch (r.Type)
            {
                case OsmElementTypes.Node:     dirtyNodeIds.Add(r.Id);     break;
                case OsmElementTypes.Way:      dirtyWayIds.Add(r.Id);      break;
                case OsmElementTypes.Relation: dirtyRelationIds.Add(r.Id); break;
            }
        }

        foreach (long wayId in dirtyWayIds)
        {
            if (state.Ways.TryGetValue(wayId, out OsmWay? w))
                foreach (long nodeId in w.NodeIds)
                    dirtyNodeIds.Add(nodeId);
        }

        List<DraftNode> nodes = [];
        foreach (long id in dirtyNodeIds)
        {
            if (state.Nodes.TryGetValue(id, out OsmNode? n))
                nodes.Add(new DraftNode
                {
                    Id = n.Id, Version = n.Version, ChangesetId = n.ChangesetId,
                    Lat = n.Lat, Lon = n.Lon, Tags = new Dictionary<string, string>(n.Tags)
                });
        }

        List<DraftWay> ways = [];
        foreach (long id in dirtyWayIds)
        {
            if (state.Ways.TryGetValue(id, out OsmWay? w))
                ways.Add(new DraftWay
                {
                    Id = w.Id, Version = w.Version, ChangesetId = w.ChangesetId,
                    NodeIds = [.. w.NodeIds], Tags = new Dictionary<string, string>(w.Tags)
                });
        }

        List<DraftRelation> relations = [];
        foreach (long id in dirtyRelationIds)
        {
            if (state.Relations.TryGetValue(id, out OsmRelation? r))
                relations.Add(new DraftRelation
                {
                    Id = r.Id, Version = r.Version, ChangesetId = r.ChangesetId,
                    Members = [.. r.Members.Select(m => new DraftMember
                    {
                        Type = (int)m.Type, Ref = m.Ref, Role = m.Role
                    })],
                    Tags = new Dictionary<string, string>(r.Tags)
                });
        }

        List<DraftEditState> editStates = [];
        foreach ((OsmElementRef r, EditState es) in state.EditStates)
        {
            if (es != EditState.Fetched)
                editStates.Add(new DraftEditState { Type = (int)r.Type, Id = r.Id, State = (int)es });
        }

        EditBufferDraft draft = new()
        {
            SavedAt = DateTimeOffset.UtcNow,
            DirtyCount = editStates.Count,
            NextNegativeId = state.NextNegativeId,
            Nodes = nodes,
            Ways = ways,
            Relations = relations,
            EditStates = editStates,
            ImageryUsed = [.. state.ImageryUsed]
        };

        await _storage.SaveDraftAsync(draft);
    }

    #endregion

    #region IDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        _saveCts?.Cancel();
        _fetchCts.Cancel();
        _fetchCts.Dispose();
        _editState.StateChanged -= OnEditBufferChanged;
        _mapState.StateChanged -= OnMapStateChanged;
        _selectionState.StateChanged -= OnSelectionChanged;
    }

    #endregion
}
