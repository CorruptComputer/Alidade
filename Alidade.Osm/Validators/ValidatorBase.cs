namespace Alidade.Osm.Validators;

/// <summary>
///   A single stateless validation check that inspects a snapshot of the edit buffer
///   and yields any issues found. Validators are run sequentially on the background
///   thread by <c>ValidationService</c>.
/// </summary>
public abstract class ValidatorBase
{
    /// <summary>
    ///   The name of this validator, used in ValidationIssue records. Should be unique
    /// </summary>
    public abstract string ValidatorName { get; }

    /// <summary>
    ///   The severity of issues this validator emits.
    /// </summary>
    public abstract ValidationSeverities Severity { get; }

    /// <summary>
    ///   Inspects the given snapshot and returns zero or more <see cref="ValidationIssue"/> records.
    /// </summary>
    /// <param name="snapshot">The snapshot of the edit buffer to inspect.</param>
    /// <param name="presets">The preset service to use for validation.</param>
    /// <returns>An enumerable of <see cref="ValidationIssue"/> records.</returns>
    public abstract IEnumerable<ValidationIssue> Check(EditBufferSnapshot snapshot, PresetService presets);
}
