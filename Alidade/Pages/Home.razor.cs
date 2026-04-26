using Alidade.Components.Dialogs;

namespace Alidade.Pages;

/// <summary>
///   Root editor page.
/// </summary>
public partial class Home(MapStateService mapState) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
        => mapState.StateChanged += OnStateChanged;

    private void OnStateChanged(object? sender, EventArgs e)
        => StateHasChanged();

    /// <inheritdoc />
    public void Dispose()
        => mapState.StateChanged -= OnStateChanged;
}
