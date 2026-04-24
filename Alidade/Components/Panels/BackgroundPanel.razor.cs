using Alidade.Map.Handlers;
using Alidade.Osm.Models.Imagery;
using Microsoft.AspNetCore.Components.Web;

namespace Alidade.Components.Panels;

/// <summary>
///   Panel for selecting the background imagery layer displayed on the map.
/// </summary>
public partial class BackgroundPanel(
    MapStateService mapState,
    IMediator mediator,
    ImageryService imagery) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
        => mapState.StateChanged += OnStateChanged;

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    /// <inheritdoc />
    public void Dispose()
        => mapState.StateChanged -= OnStateChanged;

    private const string BingId = "__bing__";
    private const string CustomId = "__custom__";

    private string? _activeId = BingId;
    private string _query = string.Empty;
    private bool _customExpanded;
    private string _customUrl = string.Empty;

    private IReadOnlyList<ImageryEntry> DisplayList
    {
        get
        {
            if (!imagery.IsLoaded)
            {
                return [];
            }

            MapBounds? bounds = mapState.State.CurrentBounds;
            IReadOnlyList<ImageryEntry> locationList = bounds is not null
                ? imagery.GetForLocation(
                    (bounds.South + bounds.North) / 2.0,
                    (bounds.West + bounds.East) / 2.0)
                : imagery.All;

            if (string.IsNullOrWhiteSpace(_query))
            {
                return locationList;
            }

            string q = _query.Trim();
            return locationList
                .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void ExpandCustom()
        => _customExpanded = !_customExpanded;

    private async Task OnCustomKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await ApplyCustom();
        }
    }

    private async Task ApplyCustom()
    {
        string url = _customUrl.Trim();
        if (string.IsNullOrEmpty(url))
        {
            return;
        }

        _activeId = CustomId;
        _customExpanded = false;
        await mediator.Send(new SetBackgroundImagery.Command([url], 256, null, false));
        await mediator.Send(new Handlers.Map.ToggleBackgroundPanel.Command());
    }

    private async Task SelectImagery(ImageryEntry entry)
    {
        (string[] tiles, bool isTms) = entry.ToMaplibreUrls();

        if (tiles.Length == 0)
        {
            return;
        }

        _activeId = entry.Id;
        _customExpanded = false;
        await mediator.Send(new SetBackgroundImagery.Command(tiles, entry.TileSize ?? 256, entry.TermsText, isTms, entry.MaxZoom));
        await mediator.Send(new Handlers.Map.ToggleBackgroundPanel.Command());
    }

    private async Task SelectBing()
    {
        _activeId = BingId;
        _customExpanded = false;
        await mediator.Send(new SetBackgroundBing.Command());
        await mediator.Send(new Handlers.Map.ToggleBackgroundPanel.Command());
    }

    private async Task Close()
        => await mediator.Send(new Handlers.Map.ToggleBackgroundPanel.Command());
}
