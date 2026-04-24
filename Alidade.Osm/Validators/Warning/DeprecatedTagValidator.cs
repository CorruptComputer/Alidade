using Alidade.Osm.Models.Tagging;

namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when an element carries a tag combination listed in the id-tagging-schema
///   deprecation rules, and offers an auto-fix to apply the suggested replacement tags.
/// </summary>
public class DeprecatedTagValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "deprecated_tag";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmNode node in snap.Nodes.Values)
        {
            foreach (DeprecationRule rule in presets.FindDeprecations(node.Tags))
            {
                yield return DeprecationIssue(node.Ref, node.Id, "node", rule.Old, node.Tags, rule.Replace);
            }
        }

        foreach (OsmWay way in snap.Ways.Values)
        {
            foreach (DeprecationRule rule in presets.FindDeprecations(way.Tags))
            {
                yield return DeprecationIssue(way.Ref, way.Id, "way", rule.Old, way.Tags, rule.Replace);
            }
        }
    }

    private ValidationIssue DeprecationIssue(
        OsmElementRef r,
        long id,
        string type,
        IReadOnlyDictionary<string, string> oldPattern,
        IReadOnlyDictionary<string, string> actualTags,
        IReadOnlyDictionary<string, string>? replace)
    {
        // Resolve wildcard values in the old pattern to the actual tag values so the
        // message reads "highway=tertiary" rather than "highway=*".
        string oldStr = string.Join(", ", oldPattern.Select(kvp =>
        {
            string actual = kvp.Value == "*" && actualTags.TryGetValue(kvp.Key, out string? v)
                ? v
                : kvp.Value;
            return $"{kvp.Key}={actual}";
        }));

        string suggestion = replace is null
            ? "remove this tag"
            : string.Join(", ", replace.Select(kv =>
            {
                // Resolve $1 back-references to the first wildcard's actual value.
                string val = kv.Value;
                if (val.StartsWith('$'))
                {
                    string wildcardKey = oldPattern
                        .FirstOrDefault(p => p.Value == "*").Key;
                    if (wildcardKey is not null && actualTags.TryGetValue(wildcardKey, out string? wv))
                    {
                        val = wv;
                    }
                }
                return $"{kv.Key}={val}";
            }));

        return new(Severity, ValidatorName,
            $"{type} {id}: {oldStr} is deprecated. Use: {suggestion}", r, false);
    }
}
