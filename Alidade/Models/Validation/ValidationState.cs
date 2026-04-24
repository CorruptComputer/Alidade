namespace Alidade.Models.Validation;

/// <summary>
///   Holds the current set of validation issues, panel visibility, and whether a
///   background validation pass is currently running.
/// </summary>
public record ValidationState(ImmutableList<ValidationIssue> Issues, bool PanelVisible, bool IsRunning)
{
    /// <summary>
    ///   Initializes with no issues, the panel hidden, and no validation in progress.
    /// </summary>
    public ValidationState() : this([], false, false) { }

    /// <summary>
    ///   Returns <see langword="true"/> when the current issue list contains at least one error.
    /// </summary>
    public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverities.Error);
}
