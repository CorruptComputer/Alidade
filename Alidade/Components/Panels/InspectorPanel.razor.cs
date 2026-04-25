using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Alidade.Osm.Models.Nsi;
using Alidade.Osm.Models.Tagging;
using Microsoft.AspNetCore.Components.Web;
using Alidade.Handlers.Map;
using Alidade.Handlers.Selection;
using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Handlers.Tagging;

namespace Alidade.Components.Panels;

/// <summary>
///   Displays tags, preset fields, and preset search for the selected or pinned element.
///   When <see cref="PinnedRef"/> is set the panel is locked to that element; otherwise
///   it follows the active selection.
/// </summary>
public partial class InspectorPanel(
    PresetService presetService,
    NsiService nsiService,
    IMediator mediator,
    SelectionStateService selectionState,
    EditBufferStateService editBufferState,
    IJSRuntime js) : IDisposable
{
    /// <summary>
    ///   When set, the panel is pinned to this element and does not follow selection changes.
    /// </summary>
    [Parameter]
    public OsmElementRef? PinnedRef { get; set; }

    private bool IsPinned => PinnedRef is not null;

    private string PanelClass
        => IsPinned
            ? "inspector-panel--pinned"
            : (_targetRef is null ? "inspector-panel-hidden" : string.Empty);

    private OsmElementRef? _targetRef;
    private ImmutableHashSet<OsmElementRef> _lastSelected = [];

    private string _elementLabel = string.Empty;
    private Preset? _activePreset;

    private string _query = string.Empty;
    private bool _isSearching;
    private List<Preset> _results = [];
    private List<NsiItem> _nsiResults = [];
    private NsiItem? _matchedNsiBrand;
    private bool _nsiBrandIncomplete;
    private ElementReference _searchInput;

    private ElementReference _panelEl;
    private ElementReference _headerEl;
    private bool _dragInitialized;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        editBufferState.StateChanged += OnBufferChanged;

        if (IsPinned)
        {
            _targetRef = PinnedRef;
        }
        else
        {
            selectionState.StateChanged += OnSelectionChanged;
            _lastSelected = selectionState.State.Selected;
            _targetRef = selectionState.State.SingleSelected;
        }

        RefreshLabel();
        RefreshPreset();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_dragInitialized)
        {
            try
            {
                await js.InvokeVoidAsync("makePanelDraggable", _panelEl, _headerEl);
                _dragInitialized = true;
            }
            catch
            {
                // JS not ready yet
            }
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        ImmutableHashSet<OsmElementRef> newSelected = selectionState.State.Selected;
        if (ReferenceEquals(newSelected, _lastSelected)) return;
        _lastSelected = newSelected;
        _targetRef = selectionState.State.SingleSelected;
        _query = string.Empty;
        _isSearching = false;
        _results = [];
        _nsiResults = [];
        RefreshLabel();
        RefreshPreset();
        StateHasChanged();
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        if (_targetRef is null) return;
        RefreshPreset();
        StateHasChanged();
    }

    private void RefreshLabel()
    {
        if (_targetRef is null)
        {
            _elementLabel = (!IsPinned && selectionState.State.Selected.Count > 1)
                ? $"{selectionState.State.Selected.Count} elements selected"
                : string.Empty;
            return;
        }

        string typeName = _targetRef.Type switch
        {
            OsmElementTypes.Node => "Node",
            OsmElementTypes.Way => "Way",
            OsmElementTypes.Relation => "Relation",
            _ => "Element"
        };
        _elementLabel = $"{typeName} {_targetRef.Id}";
    }

    private void RefreshPreset()
    {
        if (_targetRef is null)
        {
            _activePreset = null;
            return;
        }

        EditBufferState buf = editBufferState.State;
        IReadOnlyDictionary<string, string>? tags = _targetRef.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(_targetRef.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(_targetRef.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(_targetRef.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

        if (tags is null)
        {
            _activePreset = null;
            return;
        }

        // "vertex" = node that belongs to at least one live way; "point" = standalone node
        bool isVertex = _targetRef.Type == OsmElementTypes.Node
            && buf.Ways.Values.Any(w =>
                buf.EditStates.GetValueOrDefault(w.Ref) != EditState.Deleted
                && w.NodeIds.Contains(_targetRef.Id));

        string geometry = _targetRef.Type switch
        {
            OsmElementTypes.Node when isVertex => "vertex",
            OsmElementTypes.Node => "point",
            OsmElementTypes.Way when buf.Ways.TryGetValue(_targetRef.Id, out OsmWay? w) && w.IsClosed => "area",
            OsmElementTypes.Way => "line",
            OsmElementTypes.Relation => "relation",
            _ => "point"
        };

        _activePreset = presetService.BestMatch(tags, geometry);

        _matchedNsiBrand = null;
        _nsiBrandIncomplete = false;
        if (tags.TryGetValue("name", out string? nameVal) && !string.IsNullOrEmpty(nameVal))
        {
            IReadOnlyList<NsiItem> matches = nsiService.Suggest(nameVal, string.Empty, string.Empty);
            int bestScore = 0;
            foreach (NsiItem candidate in matches)
            {
                int score = NsiService.CountTagMatches(candidate, tags);
                if (score > bestScore)
                {
                    bestScore = score;
                    _matchedNsiBrand = candidate;
                    _nsiBrandIncomplete = NsiService.AnyAddTagMissing(candidate, tags);
                }
            }
        }
    }

    private void StartPresetSearch()
    {
        _query = string.Empty;
        _isSearching = true;
        _results = [];
        _nsiResults = [];
        StateHasChanged();
        _ = Task.Delay(20).ContinueWith(_ => InvokeAsync(async () =>
        {
            try { await _searchInput.FocusAsync(); }
            catch { }
        }));
    }

    private void OnQueryChanged(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_query))
        {
            _results = [];
            _nsiResults = [];
        }
        else
        {
            _results = [.. presetService.Search(_query)];
            _nsiResults = [.. nsiService.Suggest(_query, string.Empty, string.Empty)];
        }
    }

    private void OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            CancelSearch();
        }
    }

    private void CancelSearch()
    {
        _query = string.Empty;
        _isSearching = false;
        _results = [];
        _nsiResults = [];
    }

    private void ApplyNsiItem(NsiItem item)
    {
        if (_targetRef is null) return;

        EditBufferState buf = editBufferState.State;
        IReadOnlyDictionary<string, string>? currentTags = _targetRef.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(_targetRef.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(_targetRef.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(_targetRef.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

        if (currentTags is null) return;

        Dictionary<string, string> merged = new(currentTags);
        foreach ((string k, string v) in item.AddTags) merged[k] = v;

        _ = mediator.Send(new UpdateTags.Command(_targetRef, currentTags, merged));
        _query = string.Empty;
        _isSearching = false;
        _results = [];
        _nsiResults = [];
        RefreshPreset();
    }

    private async Task ApplyPreset(Preset preset)
    {
        if (_targetRef is null)
        {
            return;
        }

        EditBufferState buf = editBufferState.State;
        IReadOnlyDictionary<string, string>? currentTags = _targetRef.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(_targetRef.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(_targetRef.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(_targetRef.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

        if (currentTags is null)
        {
            return;
        }

        Dictionary<string, string>? merged = await mediator.Send(new MergePresetTags.Query(currentTags, preset));
        if (merged is null)
        {
            return;
        }

        await mediator.Send(new UpdateTags.Command(_targetRef, currentTags, merged));
        _activePreset = preset;
        _query = string.Empty;
        _isSearching = false;
        _results = [];
        _nsiResults = [];
    }

    private void PinCurrent()
    {
        if (_targetRef is not null)
        {
            _ = mediator.Send(new PinInspector.Command(_targetRef));
        }
    }

    private void OnClose()
    {
        if (IsPinned)
        {
            _ = mediator.Send(new UnpinInspector.Command(PinnedRef!));
        }
        else
        {
            _ = mediator.Send(new ClearSelection.Command());
        }
    }

    private static string GetNsiPrimaryTag(NsiItem item)
    {
        foreach (string key in new[] { "amenity", "shop", "office", "tourism", "leisure" })
        {
            if (item.Tags.TryGetValue(key, out string? v)) return v;
        }
        return string.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        editBufferState.StateChanged -= OnBufferChanged;
        if (!IsPinned)
        {
            selectionState.StateChanged -= OnSelectionChanged;
        }
    }
}
