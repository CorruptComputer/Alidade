namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a way has a segment shorter than 0.1 meters, which often indicates
///   an accidental double-click or a stacked node.
/// </summary>
public class ShortSegmentValidator : ValidatorBase
{
    private const double MinMeters = 0.1;
    private const double DegToRad = Math.PI / 180.0;
    private const double EarthR = 6_378_137.0;

    /// <inheritdoc />
    public override string ValidatorName => "short_segment";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (OsmWay way in snap.Ways.Values)
        {
            for (int i = 0; i < way.NodeIds.Count - 1; i++)
            {
                if (!snap.Nodes.TryGetValue(way.NodeIds[i], out OsmNode? n1)
                    || !snap.Nodes.TryGetValue(way.NodeIds[i + 1], out OsmNode? n2))
                {
                    continue;
                }

                double dLat = (n2.Lat - n1.Lat) * DegToRad;
                double dLon = (n2.Lon - n1.Lon) * DegToRad;
                double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                    + Math.Cos(n1.Lat * DegToRad) * Math.Cos(n2.Lat * DegToRad)
                    * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                double dist = 2 * EarthR * Math.Asin(Math.Sqrt(a));

                if (dist < MinMeters)
                {
                    yield return new(Severity, ValidatorName, $"Way {way.Id} has a segment shorter than {MinMeters} m", way.Ref, false);
                }
            }
        }
    }
}
