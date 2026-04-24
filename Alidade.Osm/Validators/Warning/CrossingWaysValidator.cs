namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when two highway, waterway, or railway ways cross each other without sharing
///   a node, indicating a missing intersection or a bridge/tunnel tag.
/// </summary>
public class CrossingWaysValidator : ValidatorBase
{
    private static readonly HashSet<string> _relevantTags = [
        "highway",
        "waterway",
        "railway"
    ];

    /// <inheritdoc />
    public override string ValidatorName => "crossing_ways";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach ((long id1, long id2) in GeometryService.FindCrossingWays(snap.Ways, snap.Nodes, _relevantTags))
        {
            if (!snap.Ways.TryGetValue(id1, out OsmWay? w1))
            {
                continue;
            }

            yield return new(Severity, ValidatorName, $"Way {id1} crosses way {id2} without a shared node", w1.Ref, false);
        }
    }
}
