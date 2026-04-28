using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using Alidade.Core.Models;
using Alidade.Core.Models.CQRS.Response;
using Alidade.Core.ServiceInterface;
using Alidade.Osm.Handlers.Api.Editing;
using Alidade.Osm.Models.Editing;
using Autofac;
using NetTopologySuite.Geometries;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Questy;
using Xunit;

namespace Alidade.Osm.Tests.Handlers.Api.Editing;

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

    private static readonly Bbox TestBbox = new(
        new Coordinate(-0.13, 51.51),
        new Coordinate(-0.12, 51.50));

    private Task<QueryResult<FetchBboxResult>> Send(IOsmEditingService service)
    {
        using ILifetimeScope scope = fixture.Container.BeginLifetimeScope(b =>
            b.RegisterInstance(service).As<IOsmEditingService>());

        ISender sender = scope.Resolve<ISender>();
        return sender.Send(new FetchBbox.Query(TestBbox));
    }

    [Fact]
    public async Task FetchBbox_HappyPath_ReturnsExpectedElementCounts()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
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
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
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
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
               .ReturnsForAnyArgs(Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("not xml"))));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task FetchBbox_ServiceThrows_ReturnsFail()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
               .ThrowsAsyncForAnyArgs(new HttpRequestException("network error"));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task FetchBbox_FirstCallTooLarge_SplitsAndSucceeds()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        int callCount = 0;
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
               .ReturnsForAnyArgs(_ =>
               {
                   if (Interlocked.Increment(ref callCount) == 1)
                   {
                       return Task.FromException<Stream>(
                           new HttpRequestException("too large", null, HttpStatusCode.BadRequest));
                   }
                   return Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(ValidOsmXml)));
               });

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.True(result.Success);
        Assert.NotNull(result.Result);
        Assert.Equal(2, result.Result.Nodes.Count);
        Assert.Equal(2, result.Result.Ways.Count);
        Assert.Equal(2, result.Result.Relations.Count);
    }

    [Fact]
    public async Task FetchBbox_AlwaysTooLarge_ReturnsFail()
    {
        IOsmEditingService service = Substitute.For<IOsmEditingService>();
        service.FetchBboxAsync(Arg.Any<Bbox>(), CancellationToken.None)
               .ThrowsAsyncForAnyArgs(new HttpRequestException("too large", null, HttpStatusCode.BadRequest));

        QueryResult<FetchBboxResult> result = await Send(service);

        Assert.False(result.Success);
    }
}
