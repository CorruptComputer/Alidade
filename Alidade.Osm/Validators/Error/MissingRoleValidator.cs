namespace Alidade.Osm.Validators.Error;

/// <summary>
///   Reports an error when a member of a multipolygon relation is missing its role string.
/// </summary>
public class MissingRoleValidator : ValidatorBase
{
    private static readonly HashSet<string> _requireRoles = ["multipolygon"];

    /// <inheritdoc />
    public override string ValidatorName => "missing_role";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Error;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmRelation rel in snap.Relations.Values)
        {
            if (!rel.Tags.TryGetValue("type", out string? type) || !_requireRoles.Contains(type))
            {
                continue;
            }

            foreach (OsmMember m in rel.Members.Where(m => string.IsNullOrEmpty(m.Role)))
            {
                yield return new(Severity, ValidatorName, $"Relation {rel.Id} member {m.Type}/{m.Ref} is missing a role", rel.Ref, false);
            }
        }
    }
}
