namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a locally edited way has no meaningful tags and is not a member of any relation.
///   A way with only <c>area=yes</c> is treated as untagged because <c>area=yes</c> is a
///   geometry hint, not a descriptive tag.
/// </summary>
public class UntaggedWayValidator : ValidatorBase
{
    /// <inheritdoc />
    public override string ValidatorName => "untagged_way";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        HashSet<long> wayIdsInRelations = [];

        foreach (OsmRelation relation in snap.Relations.Values)
        {
            foreach (OsmMember member in relation.Members)
            {
                if (member.Type == OsmElementTypes.Way)
                {
                    wayIdsInRelations.Add(member.Ref);
                }
            }
        }

        foreach (OsmWay way in snap.Ways.Values)
        {
            if (!snap.EditStates.TryGetValue(way.Ref, out EditState es) || es == EditState.Fetched)
            {
                continue;
            }

            bool isUntagged = way.Tags.Count == 0
                || (way.Tags.Count == 1 && way.Tags.GetValueOrDefault("area") == "yes");

            if (isUntagged && !wayIdsInRelations.Contains(way.Id))
            {
                yield return new(Severity, ValidatorName, $"Way {way.Id} has no tags", way.Ref, false);
            }
        }
    }
}
