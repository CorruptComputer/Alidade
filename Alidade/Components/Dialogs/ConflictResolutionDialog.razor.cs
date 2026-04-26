using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Handlers.Tagging;

namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog for resolving merge conflicts.
/// </summary>
public partial class ConflictResolutionDialog(IMediator mediator) : IDialog
{
    /// <inheritdoc/>
    public static string Title => "Conflicts";

    /// <summary>
    ///   True if there are any conflicts left to resolve.
    /// </summary>
    public bool HasConflicts => _conflicts.Count > 0;

    private List<ConflictItem> _conflicts = [];

    private bool AllResolved => _conflicts.All(c => c.Resolved.Count > 0);

    /// <summary>
    ///   Shows the dialog with the given conflicts.
    /// </summary>
    public void Show(IEnumerable<ConflictItem> conflicts)
    {
        _conflicts = [.. conflicts];
        StateHasChanged();
    }

    private void UseLocal(ConflictItem conflict)
    {
        conflict.Resolved = conflict.LocalTags.ToDictionary(kv => kv.Key, kv => kv.Value);
        StateHasChanged();
    }

    private void UseServer(ConflictItem conflict)
    {
        conflict.Resolved = conflict.ServerTags.ToDictionary(kv => kv.Key, kv => kv.Value);
        StateHasChanged();
    }

    private void SetResolved(ConflictItem conflict, string key, string value)
    {
        if (string.IsNullOrEmpty(value)) conflict.Resolved.Remove(key);
        else conflict.Resolved[key] = value;
        StateHasChanged();
    }

    private void ApplyResolutions()
    {
        foreach (ConflictItem c in _conflicts)
        {
            _ = mediator.Send(new UpdateTags.Command(c.ElementRef, c.LocalTags, c.Resolved));
        }
        _conflicts.Clear();
        StateHasChanged();
    }

    private void Discard()
    {
        foreach (ConflictItem c in _conflicts)
        {
            _ = mediator.Send(new RevertElement.Command(c.ElementRef, c.ServerTags));
        }
        _conflicts.Clear();
        StateHasChanged();
    }

    private static IEnumerable<string> AllKeys(ConflictItem c)
        => c.LocalTags.Keys.Union(c.ServerTags.Keys).OrderBy(k => k);

    private static string TagClass(string key, string value, IReadOnlyDictionary<string, string> other)
    {
        if (!other.TryGetValue(key, out string? otherVal)) return "conflict-tag-added";
        if (otherVal != value) return "conflict-tag-changed";
        return string.Empty;
    }
}
