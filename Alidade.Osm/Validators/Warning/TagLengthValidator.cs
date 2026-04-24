namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a tag value exceeds the 255-character limit imposed by the OSM API.
/// </summary>
public class TagLengthValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "tag_length";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmNode node in snap.Nodes.Values)
        {
            foreach (KeyValuePair<string, string> tag in node.Tags.Where(t => t.Value.Length > 255))
            {
                yield return Issue(node.Ref, node.Id, "node", tag.Key, tag.Value.Length);
            }
        }

        foreach (OsmWay way in snap.Ways.Values)
        {
            foreach (KeyValuePair<string, string> tag in way.Tags.Where(t => t.Value.Length > 255))
            {
                yield return Issue(way.Ref, way.Id, "way", tag.Key, tag.Value.Length);
            }
        }
    }

    private ValidationIssue Issue(OsmElementRef r, long id, string type, string k, int len)
        => new(Severity, ValidatorName, $"{type} {id}: value of '{k}' is {len} chars (limit: 255)", r, false);
}
