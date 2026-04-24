namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a locally edited highway is missing a name tag and does not carry
///   <c>noname=yes</c> to indicate the absence is intentional.
/// </summary>
public class MissingTagsValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "missing_tag";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmWay way in snap.Ways.Values)
        {
            if (!snap.EditStates.TryGetValue(way.Ref, out EditState es) || es == EditState.Fetched)
            {
                continue;
            }

            if (way.Tags.ContainsKey("highway")
                && !way.Tags.ContainsKey("name")
                && !way.Tags.ContainsKey("noname"))
            {
                yield return new(Severity, ValidatorName, $"Highway {way.Id} is missing a name tag", way.Ref, false);
            }
        }
    }
}