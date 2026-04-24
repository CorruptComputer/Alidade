using Alidade.Osm.Models.Tagging;

namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when an element's tags best match a preset whose expected geometry type
///   does not include the element's actual geometry type (e.g. a node tagged as an area).
/// </summary>
public class GeometryMismatchValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "geometry_mismatch";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        HashSet<long> vertexNodeIds = [];
        foreach (OsmWay w in snap.Ways.Values)
        {
            if (snap.EditStates.GetValueOrDefault(w.Ref) == EditState.Deleted)
            {
                continue;
            }

            foreach (long nodeId in w.NodeIds)
            {
                vertexNodeIds.Add(nodeId);
            }
        }

        foreach (OsmNode node in snap.Nodes.Values)
        {
            if (snap.EditStates.GetValueOrDefault(node.Ref) == EditState.Deleted)
            {
                continue;
            }

            if (!node.Tags.Any())
            {
                continue;
            }

            bool isVertex = vertexNodeIds.Contains(node.Id);

            // Check whether the node's tags match a geometry type that doesn't include
            // the actual geometry (e.g. a standalone node tagged with area-only preset).
            Preset? preset = presets.BestMatch(node.Tags, isVertex ? "vertex" : "point");
            if (preset is not null
                && preset.Geometry.Count > 0
                && !preset.Geometry.Contains("point")
                && !preset.Geometry.Contains("vertex"))
            {
                yield return new(Severity, ValidatorName,
                    $"Node {node.Id}: preset '{preset.Name ?? preset.Id}' expects {string.Join("/", preset.Geometry)}",
                    node.Ref, false);
            }

            // Standalone node whose tags best match a vertex-only preset.
            if (!isVertex)
            {
                Preset? vertexPreset = presets.BestMatch(node.Tags, "vertex");
                if (vertexPreset is not null
                    && !vertexPreset.Geometry.Contains("point")
                    && vertexPreset.Id != (preset?.Id ?? string.Empty))
                {
                    yield return new(Severity, ValidatorName,
                        $"Node {node.Id}: tags match preset '{vertexPreset.Name ?? vertexPreset.Id}' which requires a vertex (node on a way)",
                        node.Ref, false);
                }
            }
        }
    }
}
