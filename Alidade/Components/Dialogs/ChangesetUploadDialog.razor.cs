using Alidade.Osm.Handlers.Editing;

namespace Alidade.Components.Dialogs;

/// <summary>
///   Dialog for uploading the current edit buffer as one or more changesets.
/// </summary>
public partial class ChangesetUploadDialog(
    IMediator mediator,
    EditBufferStateService editBufferState,
    MapStateService mapState,
    SettingsStateService settingsState) : IDisposable
{
    private bool _uploading;
    private bool _done;
    private string _comment = string.Empty;
    private string _source = string.Empty;
    private string _hashtags = string.Empty;
    private string? _error;
    private string _summary = string.Empty;
    private List<OsmChange> _changes = [];
    private int _currentChangeset;
    private int _totalChangesets;
    private List<int> _uploadedChangesetIds = [];

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        editBufferState.StateChanged += OnStateChanged;
        mapState.StateChanged += OnMapStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void OnMapStateChanged(object? sender, EventArgs e)
    {
        if (mapState.State.UploadDialogVisible)
        {
            EndpointState endpointState = ApiEndpointCatalog.Endpoints[settingsState.State.ActiveEndpoint].State;
            if (endpointState != EndpointState.Enabled)
            {
                _ = mediator.Send(new Handlers.Map.ToggleUploadDialog.Command());
                return;
            }

            if (!_uploading && !_done)
            {
                EditBufferState state = editBufferState.State;
                _changes = [.. ChangesetSplitter.Split(state)];
                _summary = BuildSummary(state, _changes);
                _error = null;
                _uploadedChangesetIds.Clear();
            }
        }

        StateHasChanged();
    }

    private void Close()
    {
        _uploading = false;
        _done = false;
        _error = null;
        _ = mediator.Send(new Handlers.Map.ToggleUploadDialog.Command());
    }

    private async Task UploadAsync()
    {
        if (string.IsNullOrWhiteSpace(_comment)) return;

        _uploading = true;
        _error = null;
        _totalChangesets = _changes.Count;
        _currentChangeset = 0;

        Dictionary<string, string> tags = new()
        {
            ["comment"] = _comment.Trim(),
            ["created_by"] = AppInfo.UserAgent
        };

        if (!string.IsNullOrWhiteSpace(_source)) tags["source"] = _source.Trim();
        if (!string.IsNullOrWhiteSpace(_hashtags)) tags["hashtags"] = _hashtags.Trim();

        try
        {
            foreach (OsmChange change in _changes)
            {
                _currentChangeset++;
                StateHasChanged();

                QueryResult<int> createResult = await mediator.Send(new CreateChangeset.Query(tags));
                if (!createResult.Success || createResult.Result is 0)
                {
                    _error = $"Failed to create changeset: {createResult.FailReason}";
                    return;
                }

                int changesetId = createResult.Result;
                try
                {
                    QueryResult<DiffResult> uploadResult =
                        await mediator.Send(new UploadChangeset.Query(changesetId, change));

                    if (!uploadResult.Success || uploadResult.Result is null)
                    {
                        _error = $"Upload failed: {uploadResult.FailReason}";
                        return;
                    }

                    _uploadedChangesetIds.Add(changesetId);

                    IReadOnlyList<OsmElementRef> uploaded = CollectRefs(change);
                    await mediator.Publish(
                        new Handlers.EditBuffer.ApplyDiffResult.Notification(uploadResult.Result, uploaded));
                }
                finally
                {
                    await mediator.Send(new CloseChangeset.Command(changesetId));
                }
            }

            _done = true;
        }
        catch (Exception ex)
        {
            _error = $"Upload failed: {ex.Message}";
        }
        finally
        {
            _uploading = false;
        }
    }

    private static IReadOnlyList<OsmElementRef> CollectRefs(OsmChange change)
    {
        List<OsmElementRef> refs = [];

        foreach (OsmNode n in change.CreatedNodes.Concat(change.ModifiedNodes)) refs.Add(n.Ref);
        foreach (OsmWay w in change.CreatedWays.Concat(change.ModifiedWays)) refs.Add(w.Ref);
        foreach (OsmRelation r in change.CreatedRelations.Concat(change.ModifiedRelations)) refs.Add(r.Ref);
        foreach (long id in change.DeletedNodeIds) refs.Add(new OsmElementRef(OsmElementTypes.Node, id));
        foreach (long id in change.DeletedWayIds) refs.Add(new OsmElementRef(OsmElementTypes.Way, id));
        foreach (long id in change.DeletedRelationIds) refs.Add(new OsmElementRef(OsmElementTypes.Relation, id));

        return refs;
    }

    private static string BuildSummary(EditBufferState state, List<OsmChange> changes)
    {
        int nodes = 0, ways = 0, relations = 0;
        foreach ((OsmElementRef r, EditState es) in state.EditStates)
        {
            if (es == EditState.Fetched) continue;
            if (r.Type == OsmElementTypes.Node) nodes++;
            else if (r.Type == OsmElementTypes.Way) ways++;
            else if (r.Type == OsmElementTypes.Relation) relations++;
        }

        List<string> parts = [];
        if (nodes > 0) parts.Add($"{nodes} node{(nodes == 1 ? string.Empty : "s")}");
        if (ways > 0) parts.Add($"{ways} way{(ways == 1 ? string.Empty : "s")}");
        if (relations > 0) parts.Add($"{relations} relation{(relations == 1 ? string.Empty : "s")}");

        string elements = string.Join(", ", parts);
        return $"{elements} → {changes.Count} changeset{(changes.Count == 1 ? string.Empty : "s")}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        editBufferState.StateChanged -= OnStateChanged;
        mapState.StateChanged -= OnMapStateChanged;
    }
}
