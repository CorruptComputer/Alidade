using System.Text;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Handlers.Parsing;
using Alidade.Osm.Models;
using Alidade.Osm.Models.Parsing;
using Autofac;
using Questy;
using Xunit;

namespace Alidade.Osm.Tests.Handlers.Parsing;

public class ParseOsmXmlTests(OsmMediatorFixture fixture) : IClassFixture<OsmMediatorFixture>
{
    private Task<QueryResult<ParseOsmXmlResult>> Send(string xml)
    {
        using ILifetimeScope scope = fixture.Container.BeginLifetimeScope();
        ISender sender = scope.Resolve<ISender>();
        return sender.Send(new ParseOsmXml.Query(new MemoryStream(Encoding.UTF8.GetBytes(xml))));
    }

    [Fact]
    public async Task ParseOsmXml_WithNodesWaysRelations_ReturnsParsedCollections()
    {
        string xml = """
            <osm version="0.6">
              <node id="1" version="1" lat="51.5074" lon="-0.1278" />
              <node id="2" version="2" changeset="42" user="alice" timestamp="2024-01-01T00:00:00Z"
                    lat="51.5080" lon="-0.1270">
                <tag k="name" v="Test Node" />
              </node>
              <way id="10" version="1">
                <nd ref="1" />
                <nd ref="2" />
                <tag k="highway" v="residential" />
              </way>
              <relation id="100" version="1">
                <member type="way" ref="10" role="outer" />
                <tag k="type" v="multipolygon" />
              </relation>
            </osm>
            """;

        QueryResult<ParseOsmXmlResult> result = await Send(xml);

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Equal(2, result.Result.Nodes.Count);
        Assert.Single(result.Result.Ways);
        Assert.Single(result.Result.Relations);
    }

    [Fact]
    public async Task ParseOsmXml_WithNodeAttributes_MapsFieldsCorrectly()
    {
        string xml = """
            <osm version="0.6">
              <node id="99" version="3" changeset="7" user="bob"
                    timestamp="2023-06-15T12:00:00Z" lat="48.8566" lon="2.3522">
                <tag k="amenity" v="cafe" />
              </node>
            </osm>
            """;

        QueryResult<ParseOsmXmlResult> result = await Send(xml);

        Assert.True(result.Success);
        OsmNode node = result.Result!.Nodes[0];
        Assert.Equal(99L, node.Id);
        Assert.Equal(3, node.Version);
        Assert.Equal(7, node.ChangesetId);
        Assert.Equal("bob", node.UserName);
        Assert.Equal(48.8566, node.Lat, precision: 4);
        Assert.Equal(2.3522, node.Lon, precision: 4);
        Assert.Equal("cafe", node.Tags["amenity"]);
    }

    [Fact]
    public async Task ParseOsmXml_EmptyOsmElement_ReturnsEmptyCollections()
    {
        string xml = """<osm version="0.6"></osm>""";

        QueryResult<ParseOsmXmlResult> result = await Send(xml);

        Assert.True(result.Success);
        Assert.Empty(result.Result!.Nodes);
        Assert.Empty(result.Result.Ways);
        Assert.Empty(result.Result.Relations);
    }

    [Fact]
    public async Task ParseOsmXml_MalformedXml_ReturnsFail()
    {
        string xml = "this is not xml";

        QueryResult<ParseOsmXmlResult> result = await Send(xml);

        Assert.False(result.Success);
    }
}
