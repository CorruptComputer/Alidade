namespace Alidade.Components.Map;

/// <summary>
///   Invisible component that subscribes to viewport changes and triggers fetching of
///   OSM Notes to populate the <c>osm-notes</c> MapLibre source.
/// </summary>
public partial class NotesLayer(
    IMediator mediator,
    MapStateService mapState) : IDisposable
{
    private MapBounds? _lastFetchedBounds;
    private CancellationTokenSource _notesCts = new();

    /// <inheritdoc />
    protected override void OnInitialized()
        => mapState.StateChanged += OnMapStateChanged;

    private void OnMapStateChanged(object? sender, EventArgs e)
    {
        MapBounds? bounds = mapState.State.CurrentBounds;
        if (bounds is null || bounds.Zoom < 14)
        {
            return;
        }

        if (_lastFetchedBounds is not null && BoundsContained(bounds, _lastFetchedBounds))
        {
            return;
        }

        _notesCts.Cancel();
        _notesCts.Dispose();
        _notesCts = new CancellationTokenSource();
        _lastFetchedBounds = bounds;
        _ = mediator.Publish(new Handlers.Map.NotesFetchRequested.Notification(bounds), _notesCts.Token);
    }

    private static bool BoundsContained(MapBounds inner, MapBounds outer)
        => inner.West >= outer.West && inner.East <= outer.East
        && inner.South >= outer.South && inner.North <= outer.North;

    /// <inheritdoc />
    public void Dispose()
    {
        mapState.StateChanged -= OnMapStateChanged;
        _notesCts.Cancel();
        _notesCts.Dispose();
    }
}
