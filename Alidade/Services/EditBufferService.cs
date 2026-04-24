using System.Text.Json;
using Alidade.Handlers.EditBuffer;
using Alidade.Map.Handlers;
using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Models.Editing;
using NetTopologySuite.Features;

namespace Alidade.Services;

/// <summary>
///   Subscribes to <see cref="EditBufferStateService"/>, <see cref="SelectionStateService"/>,
///   and <see cref="MapStateService"/> changes, pushes GeoJSON <see cref="FeatureCollection"/>s
///   to MapLibre via Questy map commands, triggers OSM data fetches when the viewport moves
///   to zoom 17+, and provides the <see cref="ReplaceState"/> and <see cref="MergeFetchedData"/>
///   operations used by undo/redo and data fetch flows.
/// </summary>
public class EditBufferService : IDisposable
{
    private const double MinFetchZoom = 17.0;
    private readonly IMediator _mediator;
    private readonly NsiService _nsi;
    private readonly IndexedDBService _storage;
    private readonly JsonSerializerOptions _geoJsonOptions;
    private readonly EditBufferStateService _editState;
    private readonly MapStateService _mapState;
    private readonly SelectionStateService _selectionState;
    private readonly ValidationService? _validation;
    private readonly ILogger<EditBufferService> _log;

    // Draft persistence
    private CancellationTokenSource? _saveCts;
    private bool _hadDirtyState;

    // Selection push debouncing and delta tracking
    private int _selectionPushSeq;
    private ImmutableHashSet<OsmElementRef> _lastPushedSelected = [];
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
        JsonSerializerOptions geoJsonOptions,
        EditBufferStateService editState,
        MapStateService mapState,
        SelectionStateService selectionState,
        ILogger<EditBufferService> log,
        ValidationService? validation = null)
    {
        _mediator = mediator;
        _nsi = nsi;
        _storage = storage;
        _geoJsonOptions = geoJsonOptions;
        _editState = editState;
        _mapState = mapState;
        _selectionState = selectionState;
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
        _ = _mediator.Publish(new GeoJsonPushRequested.Notification(_editState.State));
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
            _fetchCts.Cancel();
            _fetchCts.Dispose();
            _fetchCts = new CancellationTokenSource();
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

        if (includeSelected)
        {
            await RunPushGeoJsonAsync(bufSnap);
        }

        await PushSelectionAsync(selSnap, bufSnap, includeSelected);
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

        foreach (OsmNode node in nodes)
        {
            if (!editStates.TryGetValue(node.Ref, out EditState es) || es == EditState.Fetched)
            {
                nodeDict = nodeDict.SetItem(node.Id, node);
                editStates = editStates.SetItem(node.Ref, EditState.Fetched);
            }
        }

        foreach (OsmWay way in ways)
        {
            if (!editStates.TryGetValue(way.Ref, out EditState es) || es == EditState.Fetched)
            {
                wayDict = wayDict.SetItem(way.Id, way);
                editStates = editStates.SetItem(way.Ref, EditState.Fetched);
            }
        }

        foreach (OsmRelation rel in relations)
        {
            if (!editStates.TryGetValue(rel.Ref, out EditState es) || es == EditState.Fetched)
            {
                relDict = relDict.SetItem(rel.Id, rel);
                editStates = editStates.SetItem(rel.Ref, EditState.Fetched);
            }
        }

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
        _editState.SetState(new EditBufferState());

        MapBounds? bounds = _mapState.State.CurrentBounds;
        if (bounds is not null)
        {
            _ = _mediator.Publish(new FetchBboxRequested.Notification(bounds), _fetchCts.Token);
        }
    }

    #endregion

    #region GeoJSON push

    internal async Task RunPushGeoJsonAsync(EditBufferState state)
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

        List<Feature> nodes = [];
        foreach (OsmNode n in liveNodes.Values)
        {
            Feature f = n.ToFeature();
            bool hasTags = n.Tags.Count > 0;
            bool isJunction = nodeWayCount.GetValueOrDefault(n.Id) >= 2;
            (string fill, string stroke) = (hasTags, isJunction) switch
            {
                (true,  true)  => ("#4cc", "#2aa"), // tagged junction: medium cyan
                (true,  false) => ("#9ef", "#3bb"), // tagged standalone: light cyan
                (false, true)  => ("#ccc", "#888"), // untagged junction: gray
                _              => ("#fff", "#555")  // untagged standalone: white
            };
            bool isWayNode = nodeWayCount.ContainsKey(n.Id);
            bool forceShow = selectedWayNodeIds.Contains(n.Id);
            f.Attributes["fill"]   = fill;
            f.Attributes["stroke"] = stroke;
            f.Attributes.Add("show", (hasTags || isJunction || !isWayNode || forceShow) ? "yes" : "no");
            nodes.Add(f);
        }

        List<Feature> ways = [];
        foreach (OsmWay w in state.Ways.Values)
        {
            if (state.EditStates.GetValueOrDefault(w.Ref) == EditState.Deleted)
            {
                continue;
            }

            Feature? f = w.ToFeature(liveNodes);
            if (f is not null)
            {
                ways.Add(f);
            }
        }

        await SetSourceAsync("osm-nodes", nodes);
        await SetSourceAsync("osm-ways", ways);
    }

    private async Task PushSelectionAsync(Models.Selection.SelectionState sel, EditBufferState buf, bool includeSelected)
    {
        IReadOnlyDictionary<long, OsmNode> liveNodes = buf.Nodes
            .Where(kv => buf.EditStates.GetValueOrDefault(kv.Value.Ref) != EditState.Deleted)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        if (includeSelected)
        {
            List<Feature> selectedFeatures = [];
            List<Feature> vertexFeatures = [];

            foreach (OsmElementRef elemRef in sel.Selected)
            {
                switch (elemRef.Type)
                {
                    case OsmElementTypes.Node when buf.Nodes.TryGetValue(elemRef.Id, out OsmNode? n):
                        selectedFeatures.Add(n.ToFeature());
                        break;

                    case OsmElementTypes.Way when buf.Ways.TryGetValue(elemRef.Id, out OsmWay? w)
                        && buf.EditStates.GetValueOrDefault(w.Ref) != EditState.Deleted:
                        {
                            Feature? wayFeature = w.ToFeature(liveNodes);
                            if (wayFeature is not null)
                            {
                                selectedFeatures.Add(wayFeature);
                            }

                            HashSet<long> seen = [];
                            foreach (long nodeId in w.NodeIds)
                            {
                                if (!seen.Add(nodeId)) continue;
                                if (!buf.Nodes.TryGetValue(nodeId, out OsmNode? vn)) continue;
                                if (buf.EditStates.GetValueOrDefault(vn.Ref) == EditState.Deleted) continue;
                                Feature vf = vn.ToFeature();
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
                                    Feature? mf = mw.ToFeature(liveNodes);
                                    if (mf is not null) selectedFeatures.Add(mf);
                                }
                            }
                            break;
                        }
                }
            }

            await SetSourceAsync("osm-selected", selectedFeatures);
            await SetSourceAsync("osm-vertices", vertexFeatures);
        }

        List<Feature> hoverFeatures = [];
        // Use the live hover state rather than the snapshot to avoid restoring a hover
        // that was cleared by a concurrent push while PushGeoJsonAsync was awaited.
        OsmElementRef? currentHovered = _selectionState.State.Hovered;
        if (currentHovered is not null)
        {
            switch (currentHovered.Type)
            {
                case OsmElementTypes.Node when buf.Nodes.TryGetValue(currentHovered.Id, out OsmNode? hn):
                    hoverFeatures.Add(hn.ToFeature());
                    break;
                case OsmElementTypes.Way when buf.Ways.TryGetValue(currentHovered.Id, out OsmWay? hw):
                    {
                        Feature? hf = hw.ToFeature(liveNodes);
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

        await SetSourceAsync("osm-hover", hoverFeatures);
    }

    private async Task SetSourceAsync(string sourceId, IReadOnlyList<NetTopologySuite.Features.IFeature> features)
    {
        if (features.Count == 0)
        {
            await _mediator.Send(new SetSourceData.Command(sourceId, "{\"type\":\"FeatureCollection\",\"features\":[]}"));
            return;
        }

        System.Text.StringBuilder sb = new();
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");
        for (int i = 0; i < features.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(JsonSerializer.Serialize(features[i], _geoJsonOptions));
        }
        sb.Append("]}");

        await _mediator.Send(new SetSourceData.Command(sourceId, sb.ToString()));
    }

    #endregion

    #region Data fetch

    internal async Task RunFetchBboxAsync(MapBounds bounds, IMediator mediator, CancellationToken ct)
    {
        if (bounds.Zoom < MinFetchZoom)
        {
            return;
        }

        try
        {
            FetchBboxResult? fetched = await mediator.Send(
                new FetchBbox.Query(bounds.West, bounds.South, bounds.East, bounds.North), ct);

            if (fetched is not null)
            {
                MergeFetchedData(
                    (IReadOnlyList<OsmNode>)fetched.Nodes,
                    (IReadOnlyList<OsmWay>)fetched.Ways,
                    (IReadOnlyList<OsmRelation>)fetched.Relations);
            }
        }
        catch (OperationCanceledException)
        {
            // Fetch cancelled by Clear() during an endpoint switch
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
            SavedAt      = DateTimeOffset.UtcNow,
            DirtyCount   = editStates.Count,
            NextNegativeId = state.NextNegativeId,
            Nodes        = nodes,
            Ways         = ways,
            Relations    = relations,
            EditStates   = editStates
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
