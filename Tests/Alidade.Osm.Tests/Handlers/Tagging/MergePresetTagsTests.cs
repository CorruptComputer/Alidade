using Alidade.Core.Models.CQRS.Response;
using Alidade.Osm.Handlers.Tagging;
using Alidade.Osm.Models.Tagging;
using Autofac;
using Questy;
using Xunit;

namespace Alidade.Osm.Tests.Handlers.Tagging;

public class MergePresetTagsTests(OsmMediatorFixture fixture) : IClassFixture<OsmMediatorFixture>
{
    private static Preset MakePreset(params (string k, string v)[] tags)
        => new(
            Id: "test/preset", Icon: null,
            Fields: [], MoreFields: [],
            Geometry: ["area"],
            Tags: tags.ToDictionary(t => t.k, t => t.v),
            MatchScore: 1.0, Name: null,
            Terms: [], Aliases: [],
            Searchable: true);

    private Task<QueryResult<Dictionary<string, string>>> Send(
        IReadOnlyDictionary<string, string> currentTags, Preset preset)
    {
        using ILifetimeScope scope = fixture.Container.BeginLifetimeScope();
        ISender sender = scope.Resolve<ISender>();
        return sender.Send(new MergePresetTags.Query(currentTags, preset));
    }

    [Fact]
    public async Task AreaImplyingKeyAdded_RemovesAreaYes()
    {
        Dictionary<string, string> current = new() { ["area"] = "yes" };
        QueryResult<Dictionary<string, string>> result = await Send(current, MakePreset(("landuse", "grass")));
        Assert.Equal("grass", result.Result!["landuse"]);
        Assert.False(result.Result!.ContainsKey("area"));
    }

    [Fact]
    public async Task NoAreaYes_MergesCleanly()
    {
        Dictionary<string, string> current = new() { ["name"] = "Park" };
        QueryResult<Dictionary<string, string>> result = await Send(current, MakePreset(("landuse", "grass")));
        Assert.Equal("grass", result.Result!["landuse"]);
        Assert.Equal("Park", result.Result!["name"]);
    }

    [Fact]
    public async Task AreaNo_IsNotRemoved()
    {
        Dictionary<string, string> current = new() { ["area"] = "no" };
        QueryResult<Dictionary<string, string>> result = await Send(current, MakePreset(("landuse", "grass")));
        Assert.Equal("no", result.Result!["area"]);
    }

    [Fact]
    public async Task WildcardValue_IsNotApplied()
    {
        Dictionary<string, string> current = new() { ["shop"] = "bakery" };
        QueryResult<Dictionary<string, string>> result = await Send(current, MakePreset(("shop", "*")));
        Assert.Equal("bakery", result.Result!["shop"]);
    }

    [Fact]
    public async Task AreaImplyingKeyInCurrentTags_RemovesAreaYes()
    {
        // area=yes is redundant even when the implying key comes from currentTags, not the preset
        Dictionary<string, string> current = new() { ["building"] = "yes", ["area"] = "yes" };
        QueryResult<Dictionary<string, string>> result = await Send(current, MakePreset(("name", "Town Hall")));
        Assert.False(result.Result!.ContainsKey("area"));
    }
}
