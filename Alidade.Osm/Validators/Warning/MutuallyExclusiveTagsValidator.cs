namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when an element carries two tags that are logically contradictory,
///   such as <c>name</c> together with <c>noname=yes</c>.
/// </summary>
public class MutuallyExclusiveTagsValidator : ValidatorBase
{
    private static readonly (string Key1, string Key2, string Val2)[] _rules =
    [
        ("name", "noname", "yes"),
        ("oneway", "junction", "roundabout"),
    ];

    /// <inheritdoc />
    public override string ValidatorName => "mutually_exclusive";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmWay way in snap.Ways.Values)
        {
            foreach ((string key1, string key2, string val2) in _rules)
            {
                if (way.Tags.ContainsKey(key1)
                    && way.Tags.TryGetValue(key2, out string? v)
                    && v == val2)
                {
                    yield return new(Severity, ValidatorName, $"Way {way.Id} has mutually exclusive tags: {key1} and {key2}={val2}", way.Ref, false);
                }
            }
        }

        foreach (OsmNode node in snap.Nodes.Values)
        {
            foreach ((string key1, string key2, string val2) in _rules)
            {
                if (node.Tags.ContainsKey(key1)
                    && node.Tags.TryGetValue(key2, out string? v)
                    && v == val2)
                {
                    yield return new(Severity, ValidatorName, $"Node {node.Id} has mutually exclusive tags: {key1} and {key2}={val2}", node.Ref, false);
                }
            }
        }
    }
}
