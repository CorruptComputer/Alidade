using System.Text.Json;
using Alidade.Map.Handlers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Components.Dialogs;

/// <summary>
///   Floating, draggable panel for splitting a selected closed way into a grid of equal
///   rectangular sub-areas. Shows a live orange dashed preview overlay while open.
///   Follows the same always-in-DOM, CSS-visibility pattern as <see cref="Alidade.Components.Panels.InspectorPanel"/>.
/// </summary>
public partial class GridifyDialog(
    IMediator mediator,
    MapStateService mapState,
    SelectionStateService selectionState,
    EditBufferStateService editBufferState,
    JsonSerializerOptions geoJsonOptions,
    GeometryFactory geomFactory,
    IJSRuntime js) : IDisposable
{

    private ElementReference _panelEl;
    private ElementReference _headerEl;
    private bool _dragInitialized;

    private bool _visible;
    private long? _wayId;
    private int _rows = 1;
    private int _cols = 1;
    private double _rotation;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        mapState.StateChanged += OnMapStateChanged;
        selectionState.StateChanged += OnSelectionChanged;
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
            catch (JSException)
            {
                // Map not yet initialized; will retry on next render.
            }
        }
    }

    private void OnMapStateChanged(object? sender, EventArgs e)
    {
        bool wasVisible = _visible;
        _visible = mapState.State.GridifyDialogVisible;

        if (_visible && !wasVisible)
        {
            RefreshSelection();
            _ = PushPreviewAsync();
        }
        else if (!_visible && wasVisible)
        {
            _ = ClearPreviewAsync();
        }

        StateHasChanged();
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_visible)
        {
            RefreshSelection();
            _ = PushPreviewAsync();
            StateHasChanged();
        }
    }

    private void RefreshSelection()
    {
        OsmElementRef? single = selectionState.State.SingleSelected;
        if (single is { Type: OsmElementTypes.Way })
        {
            if (_wayId != single.Id)
            {
                _wayId = single.Id;
                _rotation = GeometryService.ComputeLongestEdgeAngleDeg(
                    single.Id,
                    editBufferState.State.Ways,
                    editBufferState.State.Nodes);
            }
        }
        else
        {
            _wayId = null;
        }
    }

    private void OnRowsInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int v) && v >= 1 && v <= 99)
        {
            _rows = v;
            _ = PushPreviewAsync();
        }
    }

    private void OnColsInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int v) && v >= 1 && v <= 99)
        {
            _cols = v;
            _ = PushPreviewAsync();
        }
    }

    private void OnRotationInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out double v))
        {
            _rotation = v;
            _ = PushPreviewAsync();
        }
    }

    private async Task PushPreviewAsync()
    {
        if (_wayId is null)
        {
            await ClearPreviewAsync();
            return;
        }

        EditBufferState snapshot = editBufferState.State;

        GridifyResult result = GeometryService.Gridify(
            _wayId.Value,
            snapshot.Ways,
            snapshot.Nodes,
            _rows, _cols, _rotation);

        if (result.CellNodeRefs.Count == 0)
        {
            await ClearPreviewAsync();
            return;
        }

        FeatureCollection fc = new();

        foreach (IReadOnlyList<GridifyNodeRef> cellRefs in result.CellNodeRefs)
        {
            Coordinate[] coords = new Coordinate[cellRefs.Count];
            bool valid = true;
            for (int i = 0; i < cellRefs.Count; i++)
            {
                GridifyNodeRef nodeRef = cellRefs[i];
                if (nodeRef.IsExisting)
                {
                    if (!snapshot.Nodes.TryGetValue(nodeRef.ExistingNodeId, out OsmNode? existingNode))
                    {
                        valid = false;
                        break;
                    }
                    coords[i] = new Coordinate(existingNode.Lon, existingNode.Lat);
                }
                else
                {
                    (double lat, double lon) = result.NewNodes[nodeRef.NewNodeIndex];
                    coords[i] = new Coordinate(lon, lat);
                }
            }

            if (valid && coords.Length >= 2)
            {
                fc.Add(new Feature(geomFactory.CreateLineString(coords), new AttributesTable()));
            }
        }

        string json = JsonSerializer.Serialize(fc, geoJsonOptions);
        await mediator.Send(new SetSourceData.Command("osm-gridify-preview", json));
    }

    private async Task ClearPreviewAsync()
    {
        FeatureCollection empty = new();
        string json = JsonSerializer.Serialize(empty, geoJsonOptions);
        await mediator.Send(new SetSourceData.Command("osm-gridify-preview", json));
    }

    private async Task ApplyAsync()
    {
        if (_wayId is null)
        {
            return;
        }

        await ClearPreviewAsync();
        await mediator.Send(new GridifyWay.Command(_wayId.Value, _rows, _cols, _rotation));
        await mediator.Send(new Handlers.Map.ToggleGridifyDialog.Command());
        await mediator.Send(new Handlers.Selection.ClearSelection.Command());
    }

    private void Cancel()
    {
        _ = ClearPreviewAsync();
        _ = mediator.Send(new Handlers.Map.ToggleGridifyDialog.Command());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mapState.StateChanged -= OnMapStateChanged;
        selectionState.StateChanged -= OnSelectionChanged;
    }
}
