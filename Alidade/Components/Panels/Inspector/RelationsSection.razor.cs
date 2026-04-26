using Alidade.Osm.Handlers.Editing;
using Microsoft.AspNetCore.Components;

namespace Alidade.Components.Panels.Inspector;

/// <summary>
///   Displays the relation memberships of the inspected element and provides controls to add the
///   element to existing relations, create a new relation, reorder members, remove members, and
///   fetch undownloaded members on demand.
/// </summary>
public partial class RelationsSection(EditBufferStateService editBufferState, IMediator mediator)
    : IDisposable
{
    /// <summary>
    ///   The element whose relation memberships are displayed.
    /// </summary>
    [Parameter] public OsmElementRef? ElementRef { get; set; }

    private const double NearbyRelationRadiusMeters = 300.0;

    private bool _collapsed = false;
    private bool _showNearbyPanel;
    private bool _showNewRelationForm;
    private string _newRelationType = "multipolygon";
    private string _newMemberRole = "outer";

    private List<(OsmRelation Relation, OsmMember Membership)> _containingRelations = [];
    private List<OsmRelation> _nearbyRelations = [];
    private readonly Dictionary<long, string> _nearbyRoles = [];

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        editBufferState.StateChanged += OnBufferChanged;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        _showNearbyPanel = false;
        _showNewRelationForm = false;
        _nearbyRoles.Clear();
        Refresh();
    }

    private void OnBufferChanged(object? sender, EventArgs e)
    {
        Refresh();
        InvokeAsync(StateHasChanged);
    }

    private void Refresh()
    {
        if (ElementRef is null)
        {
            _containingRelations = [];
            _nearbyRelations = [];
            return;
        }

        EditBufferState buf = editBufferState.State;
        _containingRelations = [];
        foreach ((long _, OsmRelation relation) in buf.Relations)
        {
            if (buf.EditStates.GetValueOrDefault(relation.Ref) == EditState.Deleted) continue;
            foreach (OsmMember member in relation.Members)
            {
                if (member.Type == ElementRef.Type && member.Ref == ElementRef.Id)
                {
                    _containingRelations.Add((relation, member));
                    break;
                }
            }
        }

        if (_showNearbyPanel)
        {
            RefreshNearbyRelations(buf);
        }
    }

    private void RefreshNearbyRelations(EditBufferState buf)
    {
        if (ElementRef is null) { _nearbyRelations = []; return; }

        (double Lat, double Lon)? elementCentroid = ComputeCentroid(ElementRef, buf);
        if (elementCentroid is null) { _nearbyRelations = []; return; }

        HashSet<long> alreadyIn = [.. _containingRelations.Select(t => t.Relation.Id)];
        _nearbyRelations = [];

        foreach ((long _, OsmRelation relation) in buf.Relations)
        {
            if (alreadyIn.Contains(relation.Id)) continue;
            if (buf.EditStates.GetValueOrDefault(relation.Ref) == EditState.Deleted) continue;

            foreach (OsmMember member in relation.Members)
            {
                OsmElementRef memberRef = new(member.Type, member.Ref);
                (double Lat, double Lon)? memberCentroid = ComputeCentroid(memberRef, buf);
                if (memberCentroid is null) continue;

                double dist = HaversineMeters(
                    elementCentroid.Value.Lat, elementCentroid.Value.Lon,
                    memberCentroid.Value.Lat, memberCentroid.Value.Lon);

                if (dist <= NearbyRelationRadiusMeters)
                {
                    _nearbyRelations.Add(relation);
                    if (!_nearbyRoles.ContainsKey(relation.Id))
                    {
                        _nearbyRoles[relation.Id] = DefaultRoleForRelation(relation);
                    }
                    break;
                }
            }
        }
    }

    private static (double Lat, double Lon)? ComputeCentroid(OsmElementRef elementRef, EditBufferState buf)
    {
        switch (elementRef.Type)
        {
            case OsmElementTypes.Node:
            {
                if (!buf.Nodes.TryGetValue(elementRef.Id, out OsmNode? node)) return null;
                return (node.Lat, node.Lon);
            }
            case OsmElementTypes.Way:
            {
                if (!buf.Ways.TryGetValue(elementRef.Id, out OsmWay? way)) return null;
                double latSum = 0, lonSum = 0;
                int count = 0;
                foreach (long nodeId in way.NodeIds)
                {
                    if (!buf.Nodes.TryGetValue(nodeId, out OsmNode? n)) continue;
                    latSum += n.Lat;
                    lonSum += n.Lon;
                    count++;
                }
                if (count == 0) return null;
                return (latSum / count, lonSum / count);
            }
            case OsmElementTypes.Relation:
            {
                if (!buf.Relations.TryGetValue(elementRef.Id, out OsmRelation? relation)) return null;
                double latSum = 0, lonSum = 0;
                int count = 0;
                foreach (OsmMember m in relation.Members)
                {
                    (double Lat, double Lon)? c = ComputeCentroid(new OsmElementRef(m.Type, m.Ref), buf);
                    if (c is null) continue;
                    latSum += c.Value.Lat;
                    lonSum += c.Value.Lon;
                    count++;
                }
                if (count == 0) return null;
                return (latSum / count, lonSum / count);
            }
            default:
                return null;
        }
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6_371_000.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
    }

    private static string DefaultRoleForRelation(OsmRelation relation)
    {
        relation.Tags.TryGetValue("type", out string? relType);
        return relType == "multipolygon" ? "outer" : string.Empty;
    }

    private static string RelationLabel(OsmRelation relation)
    {
        if (relation.Tags.TryGetValue("name", out string? name) && !string.IsNullOrEmpty(name))
        {
            return name;
        }
        if (relation.Tags.TryGetValue("ref", out string? refVal) && !string.IsNullOrEmpty(refVal))
        {
            return refVal;
        }
        relation.Tags.TryGetValue("type", out string? relType);
        string prefix = string.IsNullOrEmpty(relType) ? "relation" : relType;
        return $"{prefix} r{relation.Id}";
    }

    private static string RelationTypeLabel(OsmRelation relation)
    {
        relation.Tags.TryGetValue("type", out string? relType);
        return string.IsNullOrEmpty(relType) ? "relation" : relType;
    }

    private void ToggleCollapsed()
    {
        _collapsed = !_collapsed;
    }

    private void ToggleNearbyPanel()
    {
        _showNearbyPanel = !_showNearbyPanel;
        _showNewRelationForm = false;
        if (_showNearbyPanel)
        {
            RefreshNearbyRelations(editBufferState.State);
        }
    }

    private void ToggleNewRelationForm()
    {
        _showNewRelationForm = !_showNewRelationForm;
        _showNearbyPanel = false;
    }

    private void GoToRelation(OsmRelation relation)
    {
        _ = mediator.Send(new Handlers.Selection.Select.Command(relation.Ref, false));
    }

    private void AddToNearbyRelation(OsmRelation relation)
    {
        if (ElementRef is null) return;
        string role = _nearbyRoles.GetValueOrDefault(relation.Id, string.Empty);
        _ = mediator.Send(new AddMemberToRelation.Command(relation.Id, ElementRef, role));
        _showNearbyPanel = false;
    }

    private void RemoveFromRelation(long relationId)
    {
        if (ElementRef is null) return;
        _ = mediator.Send(new RemoveMemberFromRelation.Command(relationId, ElementRef));
    }

    private void ConfirmNewRelation()
    {
        if (ElementRef is null) return;
        Dictionary<string, string> tags = [];
        if (!string.IsNullOrWhiteSpace(_newRelationType))
        {
            tags["type"] = _newRelationType.Trim();
        }
        _ = mediator.Send(new CreateRelation.Command(
            ElementRef, _newMemberRole.Trim(), tags));
        _showNewRelationForm = false;
        _newRelationType = "multipolygon";
        _newMemberRole = "outer";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        editBufferState.StateChanged -= OnBufferChanged;
    }
}
