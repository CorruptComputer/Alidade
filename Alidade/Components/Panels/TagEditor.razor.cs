using Microsoft.AspNetCore.Components;
using Alidade.Osm.Models.Nsi;
using Alidade.Osm.Handlers.Tagging;

namespace Alidade.Components.Panels;

/// <summary>
///   Raw key/value tag editor for an OSM element. When <see cref="ElementRef"/> is set
///   the editor targets that element; when null it follows the active selection.
/// </summary>
public partial class TagEditor(
    PresetService presetService,
    NsiService nsiService,
    IMediator mediator,
    EditBufferStateService editBufferState) : IDisposable
{
    /// <summary>
    ///   When provided, the editor targets this element regardless of the current
    ///   selection. When null, the editor follows <see cref="SelectionState"/>.
    /// </summary>
    [Parameter] public OsmElementRef? ElementRef { get; set; }

    // Injected lazily, only used when ElementRef is null (selection-driven mode).
    [Inject] private SelectionStateService SelectionState { get; set; } = null!;

    private OsmElementRef? _element;
    private Dictionary<string, string> _tags = [];

    // NSI suggestion state
    private string? _nsiKey;
    private List<NsiItem> _nsiSuggestions = [];

    private static readonly HashSet<string> NsiTriggerKeys = ["name", "brand", "operator"];

    private static string ValueOrEmpty(ChangeEventArgs e)
        => e.Value?.ToString() ?? string.Empty;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        editBufferState.StateChanged += OnEditBufferChanged;

        if (ElementRef is null)
        {
            // Selection-driven mode: follow global selection
            SelectionState.StateChanged += OnSelectionChanged;
            _element = SelectionState.State.SingleSelected;
        }
        else
        {
            _element = ElementRef;
        }

        RefreshTags();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // Called when parent re-renders with a new ElementRef (e.g. selection changed
        // in the active InspectorPanel and it passes the updated _targetRef down).
        if (ElementRef is not null && ElementRef != _element)
        {
            _element = ElementRef;
            _nsiKey = null;
            _nsiSuggestions.Clear();
            RefreshTags();
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _element = SelectionState.State.SingleSelected;
        _nsiKey = null;
        _nsiSuggestions.Clear();
        RefreshTags();
        StateHasChanged();
    }

    private void OnEditBufferChanged(object? sender, EventArgs e)
    {
        if (_element is null)
        {
            return;
        }

        RefreshTags();
        StateHasChanged();
    }

    private void RefreshTags()
    {
        if (_element is null)
        {
            _tags = [];
            return;
        }

        EditBufferState buf = editBufferState.State;

        _tags = _element.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(_element.Id, out OsmNode? n)
                => new Dictionary<string, string>(n.Tags),
            OsmElementTypes.Way when buf.Ways.TryGetValue(_element.Id, out OsmWay? w)
                => new Dictionary<string, string>(w.Tags),
            OsmElementTypes.Relation when buf.Relations.TryGetValue(_element.Id, out OsmRelation? r)
                => new Dictionary<string, string>(r.Tags),
            _ => []
        };
    }

    private void OnValueInput(string key, string value)
    {
        if (!NsiTriggerKeys.Contains(key))
        {
            _nsiKey = null;
            _nsiSuggestions.Clear();
            return;
        }

        string primaryKey = _tags.ContainsKey("amenity")
            ? "amenity"
            : _tags.ContainsKey("shop")
                ? "shop"
                : _tags.ContainsKey("tourism")
                    ? "tourism"
                    : _tags.ContainsKey("leisure")
                        ? "leisure"
                        : string.Empty;

        string primaryValue = primaryKey.Length > 0
            ? _tags.GetValueOrDefault(primaryKey, string.Empty)
            : string.Empty;

        _nsiKey = key;
        _nsiSuggestions = [.. nsiService.Suggest(value, primaryKey, primaryValue)];
        StateHasChanged();
    }

    private async Task ApplyNsiSuggestion(NsiItem suggestion)
    {
        if (_element is null)
        {
            return;
        }

        Dictionary<string, string> updated = new(_tags);
        foreach ((string k, string v) in suggestion.Tags)
        {
            updated[k] = v;
        }

        _nsiKey = null;
        _nsiSuggestions.Clear();
        await DispatchTagUpdateAsync(updated);
    }

    private async Task OnKeyChanged(string oldKey, string newKey)
    {
        if (oldKey == newKey || _element is null)
        {
            return;
        }

        Dictionary<string, string> updated = new(_tags);
        string value = updated.TryGetValue(oldKey, out string? v) ? v : string.Empty;
        updated.Remove(oldKey);
        if (!string.IsNullOrEmpty(newKey))
        {
            updated[newKey] = value;
        }

        await DispatchTagUpdateAsync(updated);
    }

    private async Task OnValueChanged(string key, string newValue)
    {
        if (_element is null)
        {
            return;
        }

        _nsiKey = null;
        _nsiSuggestions.Clear();

        Dictionary<string, string> updated = new(_tags)
        {
            [key] = newValue
        };

        await DispatchTagUpdateAsync(updated);
    }

    private async Task OnDeleteTag(string key)
    {
        if (_element is null)
        {
            return;
        }

        Dictionary<string, string> updated = new(_tags);
        updated.Remove(key);
        await DispatchTagUpdateAsync(updated);
    }

    private async Task OnAddTag()
    {
        if (_element is null)
        {
            return;
        }

        string newKey = $"key_{_tags.Count}";
        Dictionary<string, string> updated = new(_tags) { [newKey] = string.Empty };
        await DispatchTagUpdateAsync(updated);
    }

    private async Task DispatchTagUpdateAsync(Dictionary<string, string> newTags)
    {
        if (_element is null)
        {
            return;
        }

        Dictionary<string, string> oldTags = new(_tags);
        await mediator.Send(new UpdateTags.Command(_element, oldTags, newTags));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        editBufferState.StateChanged -= OnEditBufferChanged;
        if (ElementRef is null)
        {
            SelectionState.StateChanged -= OnSelectionChanged;
        }
    }
}
