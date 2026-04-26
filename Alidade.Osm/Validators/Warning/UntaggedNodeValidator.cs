namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a locally edited node has no tags and is not a vertex of any way.
///   Way vertices are geometry-only nodes and are expected to be untagged.
/// </summary>
public class UntaggedNodeValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "untagged_node";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        HashSet<long> vertexNodeIds = [];

        foreach (OsmWay way in snap.Ways.Values)
        {
            foreach (long nodeId in way.NodeIds)
            {
                vertexNodeIds.Add(nodeId);
            }
        }

        foreach (OsmNode node in snap.Nodes.Values)
        {
            if (!snap.EditStates.TryGetValue(node.Ref, out EditState es) || es == EditState.Fetched)
            {
                continue;
            }

            if (node.Tags.Count == 0 && !vertexNodeIds.Contains(node.Id))
            {
                yield return new(Severity, ValidatorName, $"Node {node.Id} has no tags", node.Ref, false);
            }
        }
    }
}
