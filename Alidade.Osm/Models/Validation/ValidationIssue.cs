namespace Alidade.Osm.Models.Validation;

/// <summary>
///   A single issue produced by a validator check.
/// </summary>
public record ValidationIssue(ValidationSeverities Severity, string Code, string Message, OsmElementRef? Target, bool HasAutoFix);
