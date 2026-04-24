namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a building polygon has corners that deviate more than 5° from a right
///   angle, and offers an auto-fix via orthogonalization.
/// </summary>
public class BuildingSquarenessValidator : ValidatorBase
{
    private const double AngleThresholdDeg = 5.0;

    /// <inheritdoc />
    public override string ValidatorName => "unsquare_building";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmWay way in snap.Ways.Values)
        {
            if (!way.Tags.ContainsKey("building") || !way.IsClosed)
            {
                continue;
            }

            List<long> nodeIds = [.. way.NodeIds.Take(way.NodeIds.Count - 1)];
            if (nodeIds.Count < 3)
            {
                continue;
            }

            bool hasUnsquareCorner = false;

            for (int i = 0; i < nodeIds.Count; i++)
            {
                if (!snap.Nodes.TryGetValue(nodeIds[(i - 1 + nodeIds.Count) % nodeIds.Count], out OsmNode? a)
                    || !snap.Nodes.TryGetValue(nodeIds[i], out OsmNode? o)
                    || !snap.Nodes.TryGetValue(nodeIds[(i + 1) % nodeIds.Count], out OsmNode? b))
                {
                    continue;
                }

                double ax = a.Lon - o.Lon;
                double ay = a.Lat - o.Lat;
                double bx = b.Lon - o.Lon;
                double by = b.Lat - o.Lat;
                double aLen = Math.Sqrt(ax * ax + ay * ay);
                double bLen = Math.Sqrt(bx * bx + by * by);

                if (aLen < 1e-10 || bLen < 1e-10)
                {
                    continue;
                }

                double dotp = (ax / aLen * bx / bLen) + (ay / aLen * by / bLen);
                double angleDeg = Math.Acos(Math.Clamp(dotp, -1, 1)) * 180.0 / Math.PI;
                double deviation = Math.Abs(angleDeg % 90.0);

                if (deviation > AngleThresholdDeg && deviation < (90.0 - AngleThresholdDeg))
                {
                    hasUnsquareCorner = true;
                    break;
                }
            }

            if (hasUnsquareCorner)
            {
                yield return new(Severity, ValidatorName, $"Building {way.Id} has non-square corners", way.Ref, true);
            }
        }
    }
}
