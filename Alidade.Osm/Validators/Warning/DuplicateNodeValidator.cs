namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when two nodes occupy the same geographic coordinates (rounded to 1 * 10^-7 degrees),
///   and offers an auto-fix to merge them.
/// </summary>
public class DuplicateNodeValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "duplicate_node";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        Dictionary<(long LatQ, long LonQ), long> seen = [];

        foreach (OsmNode node in snap.Nodes.Values)
        {
            (long, long) key = ((long)(node.Lat * 1e7), (long)(node.Lon * 1e7));

            if (seen.TryGetValue(key, out long firstId))
            {
                yield return new(Severity, ValidatorName, $"Node {node.Id} is at the same coordinates as node {firstId}", node.Ref, true);
            }
            else
            {
                seen[key] = node.Id;
            }
        }
    }
}
