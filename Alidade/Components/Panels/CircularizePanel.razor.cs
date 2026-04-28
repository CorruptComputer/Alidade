using Alidade.Handlers.Map;
using Alidade.Map.Handlers;
using Alidade.Osm.Handlers.Tools.Circularize;
using Alidade.Osm.Models.Tools.Circularize;
using Microsoft.AspNetCore.Components;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Components.Panels;

/// <summary>
///   Floating, draggable panel for fitting a selected closed way to its best-fit circle.
///   A slider controls the target vertex count, defaulting to iD's MAX_SEGMENT_LENGTH formula
///   (~4 m arc segments). Shows a live orange dashed preview overlay while open, sharing the
///   <c>osm-preview</c> source with other tool panels. Follows the same always-in-DOM,
///   CSS-visibility pattern as <see cref="InspectorPanel"/>.
/// </summary>
public partial class CircularizePanel(
    IMediator mediator,
    MapStateService mapState,
    SelectionStateService selectionState,
    EditBufferStateService editBufferState,
    GeometryFactory geomFactory) : IPanel, IDisposable
{
    /// <inheritdoc/>
    public static string Title => "Circularize";

    private bool _visible;
    private CircularizeState _state = new();
    private CircularizeResult? _lastResult;
    private int _minVertices = 3;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        mapState.StateChanged += OnMapStateChanged;
        selectionState.StateChanged += OnSelectionChanged;
    }

    private void OnMapStateChanged(object? sender, EventArgs e)
    {
        bool wasVisible = _visible;
        _visible = mapState.State.CircularizeDialogVisible;

        if (_visible && !wasVisible)
        {
            _ = OpenAsync();
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
            _ = OpenAsync();
        }
    }

    private async Task OpenAsync()
    {
        await RefreshSelectionAsync();
        await PushPreviewAsync();
        StateHasChanged();
    }

    private async Task RefreshSelectionAsync()
    {
        OsmElementRef? single = selectionState.State.SingleSelected;
        if (single is { Type: OsmElementTypes.Way }
            && editBufferState.State.Ways.TryGetValue(single.Id, out OsmWay? way)
            && way.IsClosed)
        {
            if (_state.WayId != single.Id)
            {
                _minVertices = Math.Max(way.NodeIds.Count - 1, 3);

                QueryResult<CircularizeResult> defaultQr =
                    await mediator.Send(new CircularizeWay.Query(single));
                if (defaultQr.Result is CircularizeResult defaultResult)
                {
                    int defaultCount = Math.Clamp(defaultResult.Moves.Count, _minVertices, 64);
                    _state = new CircularizeState { WayId = single.Id, VertexCount = defaultCount };
                    _lastResult = defaultResult;
                }
                else
                {
                    _state = new CircularizeState { WayId = single.Id };
                    _lastResult = null;
                }
            }
        }
        else
        {
            _state = _state with { WayId = null };
            _lastResult = null;
            _minVertices = 3;
        }
    }

    private void OnVertexCountInput(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out int v) && v >= _minVertices && v <= 64)
        {
            _lastResult = null;
            _state = _state with { VertexCount = v };
            _ = PushPreviewAsync();
        }
    }

    private async Task PushPreviewAsync()
    {
        if (_state.WayId is not long wayId)
        {
            await ClearPreviewAsync();
            return;
        }

        OsmElementRef wayRef = new(OsmElementTypes.Way, wayId);
        QueryResult<CircularizeResult> qr =
            await mediator.Send(new CircularizeWay.Query(wayRef, _state.VertexCount));
        if (qr.Result is not CircularizeResult result)
        {
            _lastResult = null;
            await ClearPreviewAsync();
            return;
        }

        _lastResult = result;
        Coordinate[] ring = [.. result.Moves.Select(m => m.New), result.Moves[0].New];
        FeatureCollection fc = [new Feature(geomFactory.CreateLineString(ring), new AttributesTable())];
        foreach ((long _, Coordinate? _, Coordinate pos) in result.Moves)
        {
            fc.Add(new Feature(geomFactory.CreatePoint(pos), new AttributesTable()));
        }
        await mediator.Send(new SetSourceData.Command(MapSourceNames.Preview, fc));
    }

    private async Task ClearPreviewAsync()
    {
        await mediator.Send(new SetSourceData.Command(MapSourceNames.Preview, []));
    }

    private async Task ApplyAsync()
    {
        if (_state.WayId is not long wayId)
        {
            return;
        }

        OsmElementRef wayRef = new(OsmElementTypes.Way, wayId);
        CircularizeResult? result = _lastResult;
        if (result is null)
        {
            QueryResult<CircularizeResult> qr =
                await mediator.Send(new CircularizeWay.Query(wayRef, _state.VertexCount));
            if (qr.Result is not CircularizeResult recomputed)
            {
                return;
            }
            result = recomputed;
        }

        await ClearPreviewAsync();
        await mediator.Send(new CommitCircularize.Command(result, wayId));
        await mediator.Send(new ToggleCircularizeDialog.Command());
        selectionState.SetState(selectionState.State with { Selected = [wayRef] });
    }

    private void Cancel()
    {
        _ = ClearPreviewAsync();
        _ = mediator.Send(new ToggleCircularizeDialog.Command());
    }

    /// <inheritdoc />
    public void Dispose()
    {
        mapState.StateChanged -= OnMapStateChanged;
        selectionState.StateChanged -= OnSelectionChanged;
    }
}
