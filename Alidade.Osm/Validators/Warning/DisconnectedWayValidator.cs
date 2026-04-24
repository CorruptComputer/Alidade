namespace Alidade.Osm.Validators.Warning;

/// <summary>
///   Warns when a highway or waterway endpoint is very close to (but not connected to)
///   another way, suggesting a missing connection.
/// </summary>
public class DisconnectedWayValidator : ValidatorBase
{
    private const double ThresholdDeg = 0.00002; // ~2m at equator

    /// <inheritdoc />
    public override string ValidatorName => "disconnected_way";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Warning;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        // Build a node -> ways index.
        Dictionary<long, List<long>> nodeWays = [];
        foreach (OsmWay way in snap.Ways.Values)
        {
            foreach (long nid in way.NodeIds)
            {
                if (!nodeWays.ContainsKey(nid))
                {
                    nodeWays[nid] = [];
                }

                nodeWays[nid].Add(way.Id);
            }
        }

        foreach (OsmWay way in snap.Ways.Values)
        {
            if (!way.Tags.ContainsKey("highway") && !way.Tags.ContainsKey("waterway"))
            {
                continue;
            }

            if (way.IsClosed)
            {
                continue;
            }

            if (snap.EditStates.GetValueOrDefault(way.Ref) == EditState.Fetched)
            {
                continue;
            }

            foreach (long endId in new[] { way.NodeIds[0], way.NodeIds[^1] })
            {
                if (!snap.Nodes.TryGetValue(endId, out OsmNode? endNode))
                {
                    continue;
                }

                if (nodeWays.TryGetValue(endId, out List<long>? connected) && connected.Count > 1)
                {
                    continue;
                }

                bool nearbyUnconnected = snap.Nodes.Values.Any(n =>
                    n.Id != endId
                    && !snap.EditStates.ContainsKey(n.Ref)
                    && Math.Abs(n.Lat - endNode.Lat) < ThresholdDeg
                    && Math.Abs(n.Lon - endNode.Lon) < ThresholdDeg);

                if (nearbyUnconnected)
                {
                    yield return new(Severity, ValidatorName, $"Way {way.Id} has a floating endpoint near another way", way.Ref, false);
                }
            }
        }
    }
}
