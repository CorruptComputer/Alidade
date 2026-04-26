using System.Globalization;
using Alidade.Handlers.Map;
using Alidade.Map.Handlers;
using Alidade.Osm.Handlers.Tools.Gridify;
using Alidade.Osm.Models.Tools.Gridify;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Components.Panels;

/// <summary>
///   Floating, draggable panel for splitting a selected closed way into a grid of sub-areas.
///   Column and row extension angles can be set independently, with an optional radius field
///   reserved for a future corner-rounding feature. Shows a live orange dashed preview overlay
///   while open. Follows the same always-in-DOM, CSS-visibility pattern as
///   <see cref="InspectorPanel"/>.
/// </summary>
public partial class GridifyPanel(
    IMediator mediator,
    MapStateService mapState,
    SelectionStateService selectionState,
    EditBufferStateService editBufferState,
    GeometryFactory geomFactory,
    IJSRuntime js) : IDisposable
{

    private ElementReference _panelEl;
    private ElementReference _headerEl;
    private bool _dragInitialized;

    private bool _visible;
    private GridifyState _state = new();
    private GridifyResult? _lastResult;

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
            if (_state.WayId != single.Id)
            {
                (double colRot, double rowRot) = GridifyAlgorithm.ComputeGridifyRotations(
                    single.Id,
                    editBufferState.State.Ways,
                    editBufferState.State.Nodes);
                _state = new GridifyState
                {
                    WayId = single.Id,
                    ColRotationDeg = colRot,
                    RowRotationDeg = rowRot,
                };
                _lastResult = null;
            }
        }
        else
        {
            _state = _state with { WayId = null };
            _lastResult = null;
        }
    }

    private void OnRowsInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int v) && v >= 1 && v <= 99)
        {
            _state = _state with { Rows = v };
            _ = PushPreviewAsync();
        }
    }

    private void OnColsInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int v) && v >= 1 && v <= 99)
        {
            _state = _state with { Cols = v };
            _ = PushPreviewAsync();
        }
    }

    private void OnRowRotationInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            _state = _state with { RowRotationDeg = v };
            _ = PushPreviewAsync();
        }
    }

    private void OnColRotationInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            _state = _state with { ColRotationDeg = v };
            _ = PushPreviewAsync();
        }
    }

    private void OnRowRadiusInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            _state = _state with { RowRadiusDeg = v };
        }
    }

    private void OnColRadiusInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
        {
            _state = _state with { ColRadiusDeg = v };
        }
    }

    // Returns a CSS transform style string that rotates the ↑ arrow to point along
    // extensionDeg (geographic CCW-from-east). ↑ points north (90°) at 0 CSS rotation,
    // so CSS rotation = -(extensionDeg - 90) = 90 - extensionDeg.
    private static string ArrowStyle(double extensionDeg)
        => $"transform: rotate({(90.0 - extensionDeg).ToString("F1", CultureInfo.InvariantCulture)}deg)";

    private async Task PushPreviewAsync()
    {
        if (_state.WayId is null)
        {
            await ClearPreviewAsync();
            return;
        }

        QueryResult<GridifyResult> queryResult = await mediator.Send(new GridifyWay.Query(_state));
        if (queryResult.Result is not GridifyResult gridifyResult)
        {
            _lastResult = null;
            await ClearPreviewAsync();
            return;
        }

        _lastResult = gridifyResult;
        EditBufferState snapshot = editBufferState.State;
        FeatureCollection fc = [];

        foreach (IReadOnlyList<GridifyNodeRef> cellRefs in gridifyResult.CellNodeRefs)
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
                    (double lat, double lon) = gridifyResult.NewNodes[nodeRef.NewNodeIndex];
                    coords[i] = new Coordinate(lon, lat);
                }
            }

            if (valid && coords.Length >= 2)
            {
                fc.Add(new Feature(geomFactory.CreateLineString(coords), new AttributesTable()));
            }
        }

        await mediator.Send(new SetSourceData.Command("osm-gridify-preview", fc));
    }

    private async Task ClearPreviewAsync()
    {
        await mediator.Send(new SetSourceData.Command("osm-gridify-preview", []));
    }

    private async Task ApplyAsync()
    {
        if (_state.WayId is not long wayId)
        {
            return;
        }

        // Reuse the last computed result when available; recompute if stale.
        GridifyResult? result = _lastResult;
        if (result is null)
        {
            QueryResult<GridifyResult> queryResult = await mediator.Send(new GridifyWay.Query(_state));
            if (queryResult.Result is not GridifyResult recomputed)
            {
                return;
            }
            result = recomputed;
        }

        await ClearPreviewAsync();

        IReadOnlyList<OsmElementRef>? cellWays = (await mediator.Send(
            new CommitGridify.Query(wayId, result))).Result;

        await mediator.Send(new ToggleGridifyDialog.Command());

        ImmutableHashSet<OsmElementRef> selection = cellWays is not null
            ? [.. cellWays]
            : [];
        selectionState.SetState(selectionState.State with { Selected = selection });
    }

    private void Cancel()
    {
        _ = ClearPreviewAsync();
        _ = mediator.Send(new ToggleGridifyDialog.Command());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mapState.StateChanged -= OnMapStateChanged;
        selectionState.StateChanged -= OnSelectionChanged;
    }
}
