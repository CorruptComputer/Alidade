using System.Text;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Handlers.Editing;
using Alidade.Osm.Models.Editing;
using Alidade.Core.ServiceInterface;
using Autofac;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questy;
using Xunit;

namespace Alidade.Osm.Tests.Handlers.Editing;

public class FetchBboxTests(OsmMediatorFixture fixture) : IClassFixture<OsmMediatorFixture>
{
    private static readonly string ValidOsmXml = """
        <osm version="0.6">
          <node id="1" version="1" lat="51.5074" lon="-0.1278" />
          <way id="10" version="1">
            <nd ref="1" />
          </way>
          <relation id="100" version="1" />
        </osm>
        """;

    private Task<QueryResult<FetchBboxResult>> Send(IOsmEditingService service)
    {
        using ILifetimeScope scope = fixture.Container.BeginLifetimeScope(b =>
            b.RegisterInstance(service).As<IOsmEditingService>());

        ISender sender = scope.Resolve<ISender>();
        return sender.Send(new FetchBbox.Query(-0.13, 51.50, -0.12, 51.51));
    }

    [Fact]
    public async Task FetchBbox_HappyPath_ReturnsExpectedElementCounts()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(default, default, default, default)
               .ReturnsForAnyArgs(Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(ValidOsmXml))));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Single(result.Result.Nodes);
        Assert.Single(result.Result.Ways);
        Assert.Single(result.Result.Relations);
    }

    [Fact]
    public async Task FetchBbox_ServiceReturnsEmptyOsm_ReturnsEmptyCollections()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(default, default, default, default)
               .ReturnsForAnyArgs(Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("""<osm version="0.6"></osm>"""))));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.True(result.Success);
        Assert.Empty(result.Result!.Nodes);
        Assert.Empty(result.Result.Ways);
        Assert.Empty(result.Result.Relations);
    }

    [Fact]
    public async Task FetchBbox_ServiceReturnsMalformedXml_ReturnsFail()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(default, default, default, default)
               .ReturnsForAnyArgs(Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("not xml"))));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task FetchBbox_ServiceThrows_ReturnsFail()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(default, default, default, default)
               .ThrowsAsyncForAnyArgs(new HttpRequestException("network error"));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.False(result.Success);
    }
}
