using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Alidade.Osm.Models.Tagging;
using Alidade.Handlers.Selection;
using Alidade.Osm.Handlers.Tagging;

namespace Alidade.Components.Panels;

/// <summary>
///   Displays merged tags, feature type, and a per-element feature list when multiple
///   OSM elements are selected. All tag edits apply atomically to every selected element
///   and produce a single undo entry.
/// </summary>
public partial class MultiSelectInspectorPanel(
    PresetService presetService,
    IMediator mediator,
    SelectionStateService selectionState,
    EditBufferStateService editBufferState) : IPanel, IDisposable
{
    /// <inheritdoc/>
    public static string Title => "Inspector";

    private ImmutableHashSet<OsmElementRef> _selected = [];
    private string? _commonGeometry;
    private string _featureTypeLabel = string.Empty;
    private Preset? _commonPreset;
    private List<FeatureItem> _features = [];
    private List<MergedTag> _mergedTags = [];

    private bool _isSearching;
    private string _query = string.Empty;
    private List<Preset> _results = [];
    private ElementReference _searchInput;

    private record FeatureItem(OsmElementRef Ref, string Label);

    private record MergedTag(string Key, string? CommonValue, bool IsConsistent);

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        selectionState.StateChanged += OnSelectionChanged;
        editBufferState.StateChanged += OnBufferChanged;
        _selected = selectionState.State.Selected;
        RefreshAll();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _selected = selectionState.State.Selected;
        _isSearching = false;
        _query = string.Empty;
        _results = [];
        RefreshAll();
        StateHasChanged();
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        if (_selected.Count <= 1) return;
        RefreshAll();
        StateHasChanged();
    }

    private void RefreshAll()
    {
        if (_selected.Count <= 1) return;
        RefreshGeometry();
        RefreshFeatureType();
        RefreshFeatureList();
        RefreshMergedTags();
    }

    private void RefreshGeometry()
    {
        EditBufferState buf = editBufferState.State;
        string? common = null;
        bool first = true;

        foreach (OsmElementRef elementRef in _selected)
        {
            string geom = GetGeometry(buf, elementRef);
            if (first)
            {
                common = geom;
                first = false;
            }
            else if (common != geom)
            {
                common = null;
                break;
            }
        }

        _commonGeometry = common;
    }

    private void RefreshFeatureType()
    {
        EditBufferState buf = editBufferState.State;
        Preset? common = null;
        bool first = true;
        bool allSame = true;

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? tags = GetTags(buf, elementRef);
            if (tags is null) { allSame = false; break; }

            Preset? preset = presetService.BestMatch(tags, GetGeometry(buf, elementRef));
            string presetId = preset?.Id ?? string.Empty;

            if (first)
            {
                common = preset;
                first = false;
            }
            else if ((common?.Id ?? string.Empty) != presetId)
            {
                allSame = false;
                break;
            }
        }

        if (allSame && common is not null)
        {
            _commonPreset = common;
            _featureTypeLabel = PresetService.FormatTagLabel(common);
        }
        else
        {
            _commonPreset = null;
            _featureTypeLabel = "Multiple Types";
        }
    }

    private void RefreshFeatureList()
    {
        EditBufferState buf = editBufferState.State;
        _features = [];

        foreach (OsmElementRef elementRef in _selected.OrderBy(e => ((int)e.Type, e.Id)))
        {
            IReadOnlyDictionary<string, string>? tags = GetTags(buf, elementRef);
            Preset? preset = tags is not null
                ? presetService.BestMatch(tags, GetGeometry(buf, elementRef))
                : null;

            string label = preset is not null
                ? PresetService.FormatTagLabel(preset)
                : elementRef.Type switch
                {
                    OsmElementTypes.Node => "Node",
                    OsmElementTypes.Way => "Way",
                    OsmElementTypes.Relation => "Relation",
                    _ => "Element"
                };

            _features.Add(new FeatureItem(elementRef, label));
        }
    }

    private void RefreshMergedTags()
    {
        EditBufferState buf = editBufferState.State;
        Dictionary<string, (HashSet<string> Values, int Count)> keyData = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? tags = GetTags(buf, elementRef);
            if (tags is null) continue;

            foreach ((string k, string v) in tags)
            {
                if (!keyData.TryGetValue(k, out (HashSet<string> Values, int Count) data))
                {
                    data = ([], 0);
                }
                data.Values.Add(v);
                keyData[k] = (data.Values, data.Count + 1);
            }
        }

        int elementCount = _selected.Count;
        _mergedTags = [];

        foreach ((string key, (HashSet<string> values, int count)) in keyData.OrderBy(x => x.Key))
        {
            bool consistent = count == elementCount && values.Count == 1;
            _mergedTags.Add(new MergedTag(key, consistent ? values.First() : null, consistent));
        }
    }

    private async Task OnValueChanged(string key, string newValue)
    {
        EditBufferState buf = editBufferState.State;
        List<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> updates = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? currentTags = GetTags(buf, elementRef);
            if (currentTags is null) continue;
            updates.Add((elementRef, new Dictionary<string, string>(currentTags) { [key] = newValue }));
        }

        if (updates.Count > 0)
        {
            await mediator.Send(new BulkUpdateTags.Command(updates));
        }
    }

    private async Task OnKeyChanged(string oldKey, string newKey)
    {
        if (oldKey == newKey) return;

        EditBufferState buf = editBufferState.State;
        List<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> updates = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? currentTags = GetTags(buf, elementRef);
            if (currentTags is null || !currentTags.ContainsKey(oldKey)) continue;

            Dictionary<string, string> updated = new(currentTags);
            string value = updated[oldKey];
            updated.Remove(oldKey);
            if (!string.IsNullOrEmpty(newKey))
            {
                updated[newKey] = value;
            }
            updates.Add((elementRef, updated));
        }

        if (updates.Count > 0)
        {
            await mediator.Send(new BulkUpdateTags.Command(updates));
        }
    }

    private async Task OnDeleteTag(string key)
    {
        EditBufferState buf = editBufferState.State;
        List<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> updates = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? currentTags = GetTags(buf, elementRef);
            if (currentTags is null || !currentTags.ContainsKey(key)) continue;

            Dictionary<string, string> updated = new(currentTags);
            updated.Remove(key);
            updates.Add((elementRef, updated));
        }

        if (updates.Count > 0)
        {
            await mediator.Send(new BulkUpdateTags.Command(updates));
        }
    }

    private async Task OnAddTag()
    {
        string newKey = $"key_{_mergedTags.Count}";
        EditBufferState buf = editBufferState.State;
        List<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> updates = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? currentTags = GetTags(buf, elementRef);
            if (currentTags is null) continue;
            updates.Add((elementRef, new Dictionary<string, string>(currentTags) { [newKey] = string.Empty }));
        }

        if (updates.Count > 0)
        {
            await mediator.Send(new BulkUpdateTags.Command(updates));
        }
    }

    private async Task ApplyPresetToAllAsync(Preset preset)
    {
        EditBufferState buf = editBufferState.State;
        List<(OsmElementRef Target, IReadOnlyDictionary<string, string> NewTags)> updates = [];

        foreach (OsmElementRef elementRef in _selected)
        {
            IReadOnlyDictionary<string, string>? currentTags = GetTags(buf, elementRef);
            if (currentTags is null) continue;

            Dictionary<string, string>? merged = await mediator.Send(new MergePresetTags.Query(currentTags, preset));
            if (merged is null) continue;

            updates.Add((elementRef, merged));
        }

        if (updates.Count > 0)
        {
            await mediator.Send(new BulkUpdateTags.Command(updates));
        }

        _commonPreset = preset;
        _featureTypeLabel = PresetService.FormatTagLabel(preset);
        _isSearching = false;
        _query = string.Empty;
        _results = [];
    }

    private void StartPresetSearch()
    {
        _query = string.Empty;
        _isSearching = true;
        _results = [];
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
        _results = string.IsNullOrWhiteSpace(_query)
            ? []
            : [.. presetService.Search(_query)];
    }

    private void OnSearchKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") CancelSearch();
    }

    private void CancelSearch()
    {
        _query = string.Empty;
        _isSearching = false;
        _results = [];
    }

    private void OnDeselectFeature(OsmElementRef elementRef)
        => _ = mediator.Send(new Select.Command(elementRef, AddToSelection: true));

    private void OnClose()
        => _ = mediator.Send(new ClearSelection.Command());

    private static IReadOnlyDictionary<string, string>? GetTags(EditBufferState buf, OsmElementRef elementRef)
        => elementRef.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(elementRef.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(elementRef.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(elementRef.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

    private string GetGeometry(EditBufferState buf, OsmElementRef elementRef)
    {
        bool isVertex = elementRef.Type == OsmElementTypes.Node
            && buf.Ways.Values.Any(w =>
                buf.EditStates.GetValueOrDefault(w.Ref) != EditState.Deleted
                && w.NodeIds.Contains(elementRef.Id));

        return elementRef.Type switch
        {
            OsmElementTypes.Node when isVertex => "vertex",
            OsmElementTypes.Node => "point",
            OsmElementTypes.Way when buf.Ways.TryGetValue(elementRef.Id, out OsmWay? w) && w.IsClosed => "area",
            OsmElementTypes.Way => "line",
            OsmElementTypes.Relation => "relation",
            _ => "point"
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        selectionState.StateChanged -= OnSelectionChanged;
        editBufferState.StateChanged -= OnBufferChanged;
    }
}
