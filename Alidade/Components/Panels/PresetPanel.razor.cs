using Alidade.Osm.Handlers.Tagging;
using Alidade.Osm.Models.Tagging;
using Microsoft.AspNetCore.Components;

namespace Alidade.Components.Panels;

/// <summary>
///   Panel for changing presets.
/// </summary>
public partial class PresetPanel(
    PresetService PresetService,
    IMediator Mediator,
    SelectionStateService SelectionState,
    EditBufferStateService EditBufferState,
    ToolStateService ToolState) : IDisposable
{
    private string _query = string.Empty;
    private int _inputKey = 0;
    private List<Preset> _results = [];
    private Preset? _activePreset;
    private ElementReference _searchInput;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        SelectionState.StateChanged += OnSelectionChanged;
        ToolState.StateChanged += OnToolStateChanged;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await Task.CompletedTask;
    }

    private void OnQueryChanged(ChangeEventArgs e)
    {
        _query = e.Value?.ToString() ?? string.Empty;
        _results = [.. PresetService.Search(_query)];
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        _results = [];
        _query = string.Empty;
        _inputKey++;
        _activePreset = ResolveActivePreset();
        StateHasChanged();
    }

    private void OnToolStateChanged(object? sender, EventArgs e)
        => StateHasChanged();

    private Preset? ResolveActivePreset()
    {
        OsmElementRef? selected = SelectionState.State.SingleSelected;
        if (selected is null)
        {
            return null;
        }

        EditBufferState buf = EditBufferState.State;
        IReadOnlyDictionary<string, string>? tags = selected.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(selected.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(selected.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(selected.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

        if (tags is null)
        {
            return null;
        }

        string geometry = selected.Type switch
        {
            OsmElementTypes.Node => "point",
            OsmElementTypes.Way when buf.Ways.TryGetValue(selected.Id, out OsmWay? w) && w.IsClosed => "area",
            OsmElementTypes.Way => "line",
            OsmElementTypes.Relation => "relation",
            _ => "point"
        };

        return PresetService.BestMatch(tags, geometry);
    }

    private async Task ApplyPreset(Preset preset)
    {
        OsmElementRef? selected = SelectionState.State.SingleSelected;
        if (selected is null)
        {
            return;
        }

        EditBufferState buf = EditBufferState.State;
        IReadOnlyDictionary<string, string>? currentTags = selected.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(selected.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(selected.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(selected.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };

        if (currentTags is null)
        {
            return;
        }

        Dictionary<string, string>? merged = await Mediator.Send(new MergePresetTags.Query(currentTags, preset));
        if (merged is null)
        {
            return;
        }

        await Mediator.Send(new UpdateTags.Command(selected, currentTags, merged));
        _activePreset = preset;
        _query = string.Empty;
        _results = [];
        _inputKey++;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        SelectionState.StateChanged -= OnSelectionChanged;
        ToolState.StateChanged -= OnToolStateChanged;
    }
}
