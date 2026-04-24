namespace Alidade.Osm.Validators.Error;

/// <summary>
///   Reports an error when a relation has more than 10,000 members, which exceeds
///   the OSM API upload limit.
/// </summary>
public class RelationSizeValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "relation_size";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Error;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmRelation rel in snap.Relations.Values)
        {
            if (rel.Members.Count > 10_000)
            {
                yield return new(Severity, ValidatorName, $"Relation {rel.Id} has {rel.Members.Count} members (limit: 10,000)", rel.Ref, false);
            }
        }
    }
}
