namespace Alidade.Components.Dialogs;

/// <summary>
///   Shown on startup when a draft of unsaved changes is found in IndexedDB.
///   Offers the user the choice to continue the previous session or discard the saved edits.
/// </summary>
public partial class RecoverDraftDialog(
    DraftStateService draftState,
    EditBufferService editBufferService,
    IMediator mediator) : IDialog, IDisposable
{
    /// <inheritdoc/>
    public static string Title => "Unsaved Changes Found";

    private bool _busy;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        draftState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private async Task Continue()
    {
        _busy = true;
        EditBufferDraft? draft = await editBufferService.TryLoadDraftAsync();
        if (draft is not null)
        {
            await mediator.Send(new RestoreDraft.Command(draft));
        }
        await mediator.Publish(new DraftResolved.Notification());
        _busy = false;
    }

    private async Task Discard()
    {
        _busy = true;
        await mediator.Publish(new DeleteDraftRequested.Notification());
        await mediator.Publish(new DraftResolved.Notification());
        _busy = false;
    }

    private static string FormatAge(DateTimeOffset? savedAt)
    {
        long ageMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (savedAt?.ToUnixTimeMilliseconds() ?? 0);
        TimeSpan age = TimeSpan.FromMilliseconds(Math.Max(0, ageMs));

        if (age.TotalMinutes < 1) return "just now";
        if (age.TotalHours < 1)
        {
            int mins = (int)age.TotalMinutes;
            return $"{mins} minute{(mins == 1 ? string.Empty : "s")} ago";
        }
        if (age.TotalDays < 1)
        {
            int hours = (int)age.TotalHours;
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }
        int days = (int)age.TotalDays;
        return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
    }

    /// <inheritdoc />
    public void Dispose()
        => draftState.StateChanged -= OnStateChanged;
}
