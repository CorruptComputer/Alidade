using System.Collections.Immutable;
using Alidade.Core.Enums;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Handlers.Tools.Circularize;
using Alidade.Osm.Models;
using Alidade.Osm.Models.EditBuffer;
using Alidade.Osm.Models.Tools.Circularize;
using Alidade.Osm.Services;
using Alidade.Osm.Services.State;
using NetTopologySuite.Geometries;
using Xunit;

namespace Alidade.Osm.Tests.Handlers.Tools;

public class CircularizeWayTests
{
    // Distance tolerance in projected meters. The algorithm is analytically exact for nodes
    // that are already on a circle, so any error is purely floating-point rounding.
    private const double Epsilon = 1e-6;

    // All tests use a circle centered here. Mid-latitude keeps the flat-Earth approximation
    // accurate and keeps coordinates well within valid WGS-84 bounds.
    private const double CenterLat = 51.5;
    private const double CenterLon = -0.1;
    private const double RadiusMeters = 100.0;

    #region On-circle tests
    [Fact]
    public async Task CircularizeWay_EquilateralTriangle_AllOutputNodesOnOriginalCircle()
    {
        // Three nodes equally spaced at 0°, 120°, 240° — the centroid of an equilateral
        // triangle inscribed in a circle equals the circle's centre, so the algorithm
        // recovers the original circle exactly and every output node must lie on it.
        (double cx, double cy, CircularizeWay handler) = BuildCircleHandler(
            [0.0, 2.0 * Math.PI / 3, 4.0 * Math.PI / 3]);

        QueryResult<CircularizeResult> qr =
            await handler.Handle(new CircularizeWay.Query(new OsmElementRef(OsmElementTypes.Way, 1L)), default);

        Assert.True(qr.Success);
        Assert.NotEmpty(qr.Result!.Moves);
        foreach ((long _, Coordinate? _, Coordinate pos) in qr.Result.Moves)
        {
            AssertOnCircle(pos, cx, cy);
        }
    }

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task CircularizeWay_EquilateralTriangle_VertexCountOverride_ProducesExactCount(int requestedCount)
    {
        (_, _, CircularizeWay handler) = BuildCircleHandler(
            [0.0, 2.0 * Math.PI / 3, 4.0 * Math.PI / 3]);

        QueryResult<CircularizeResult> qr =
            await handler.Handle(
                new CircularizeWay.Query(new OsmElementRef(OsmElementTypes.Way, 1L), requestedCount), default);

        Assert.True(qr.Success);
        Assert.Equal(requestedCount, qr.Result!.Moves.Count);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(24)]
    public async Task CircularizeWay_EquilateralTriangle_VertexCountOverride_AllOutputNodesOnOriginalCircle(int requestedCount)
    {
        (double cx, double cy, CircularizeWay handler) = BuildCircleHandler(
            [0.0, 2.0 * Math.PI / 3, 4.0 * Math.PI / 3]);

        QueryResult<CircularizeResult> qr =
            await handler.Handle(
                new CircularizeWay.Query(new OsmElementRef(OsmElementTypes.Way, 1L), requestedCount), default);

        Assert.True(qr.Success);
        foreach ((long _, Coordinate? _, Coordinate pos) in qr.Result!.Moves)
        {
            AssertOnCircle(pos, cx, cy);
        }
    }
    #endregion

    #region Failure cases
    [Fact]
    public async Task CircularizeWay_OpenWay_ReturnsFail()
    {
        OsmNode n1 = MakeNode(1L, 51.500, -0.100);
        OsmNode n2 = MakeNode(2L, 51.501, -0.100);
        OsmNode n3 = MakeNode(3L, 51.501, -0.101);
        OsmWay openWay = MakeWay(1L, [1L, 2L, 3L]); // first ≠ last → not closed

        CircularizeWay handler = new(BuildState([n1, n2, n3], openWay));

        QueryResult<CircularizeResult> qr =
            await handler.Handle(new CircularizeWay.Query(new OsmElementRef(OsmElementTypes.Way, 1L)), default);

        Assert.False(qr.Success);
    }

    [Fact]
    public async Task CircularizeWay_TwoNodeClosedWay_ReturnsFail()
    {
        OsmNode n1 = MakeNode(1L, 51.500, -0.100);
        OsmNode n2 = MakeNode(2L, 51.501, -0.100);
        OsmWay way = MakeWay(1L, [1L, 2L, 1L]); // closed, but only 2 unique nodes

        CircularizeWay handler = new(BuildState([n1, n2], way));

        QueryResult<CircularizeResult> qr =
            await handler.Handle(new CircularizeWay.Query(new OsmElementRef(OsmElementTypes.Way, 1L)), default);

        Assert.False(qr.Success);
    }
    #endregion

    #region Helpers
    private static (double cx, double cy, CircularizeWay handler) BuildCircleHandler(double[] angles)
    {
        double latRef = CenterLat;
        Coordinate centerProj = GeometryService.Project(new Coordinate(CenterLon, CenterLat), latRef);
        double cx = centerProj.X;
        double cy = centerProj.Y;

        OsmNode[] nodes = angles
            .Select((angle, i) =>
            {
                Coordinate proj = new(cx + RadiusMeters * Math.Cos(angle), cy + RadiusMeters * Math.Sin(angle));
                Coordinate latLon = GeometryService.Unproject(proj, latRef);
                return MakeNode(i + 1L, latLon.Y, latLon.X);
            })
            .ToArray();

        long[] nodeIds = [.. nodes.Select(n => n.Id), nodes[0].Id];
        OsmWay way = MakeWay(1L, nodeIds);

        return (cx, cy, new CircularizeWay(BuildState(nodes, way)));
    }

    private static EditBufferStateService BuildState(OsmNode[] nodes, OsmWay way)
    {
        EditBufferStateService svc = new();
        svc.SetState(new EditBufferState(
            nodes.ToImmutableDictionary(n => n.Id),
            ImmutableDictionary<long, OsmWay>.Empty.SetItem(way.Id, way),
            ImmutableDictionary<long, OsmRelation>.Empty,
            ImmutableDictionary<OsmElementRef, EditState>.Empty,
            -1L));
        return svc;
    }

    private static OsmNode MakeNode(long id, double lat, double lon)
        => new(id, 1, null, null, null, lat, lon, ImmutableDictionary<string, string>.Empty);

    private static OsmWay MakeWay(long id, long[] nodeIds)
        => new(id, 1, null, null, null, nodeIds, ImmutableDictionary<string, string>.Empty);

    private static void AssertOnCircle(Coordinate pos, double cx, double cy)
    {
        Coordinate proj = GeometryService.Project(new Coordinate(pos.X, pos.Y), CenterLat);
        double dist = Math.Sqrt((proj.X - cx) * (proj.X - cx) + (proj.Y - cy) * (proj.Y - cy));
        Assert.InRange(dist, RadiusMeters - Epsilon, RadiusMeters + Epsilon);
    }
    #endregion
}
