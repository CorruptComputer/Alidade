namespace Alidade.Components.Panels;

/// <summary>
///   Panel that lists validation issues for the current edit buffer, grouped by severity.
/// </summary>
public partial class ValidationPanel(
    IMediator mediator,
    ValidationStateService validationState) : IDisposable
{
    /// <inheritdoc />
    protected override void OnInitialized()
    {
        validationState.StateChanged += OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e) => StateHasChanged();

    private void Close()
        => _ = mediator.Send(new Handlers.Validation.ToggleValidationPanel.Command());

    private void SelectIssueTarget(ValidationIssue issue)
    {
        if (issue.Target is null) return;
        _ = mediator.Send(new Handlers.Selection.Select.Command(issue.Target, false));
        _ = mediator.Send(new Handlers.Map.FlyToElement.Command(issue.Target));
    }

    private void ApplyFix(ValidationIssue issue)
    {
        switch (issue.Code)
        {
            case "unsquare_building" when issue.Target is { Type: OsmElementTypes.Way }:
                _ = mediator.Publish(new Handlers.Tool.Square.Notification());
                break;
        }
    }

    private static string SeverityIcon(ValidationSeverities s)
        => s switch
        {
            ValidationSeverities.Error => "✕",
            ValidationSeverities.Warning => "⚠",
            _ => "ℹ"
        };

    /// <inheritdoc />
    public void Dispose()
        => validationState.StateChanged -= OnStateChanged;
}
