using Microsoft.AspNetCore.Components;
using Alidade.Osm.Models.Tagging;
using Alidade.Osm.Handlers.Editing;

namespace Alidade.Components.Panels;

/// <summary>
///   Renders a single preset field (check, radio, combo, number, or text) and dispatches
///   tag updates when the user changes the value.
/// </summary>
public partial class PresetField(
    IMediator mediator,
    EditBufferStateService editBufferState)
{
    /// <summary>The field definition to render.</summary>
    [Parameter, EditorRequired]
    public FieldDef Field { get; set; } = null!;

    /// <summary>The element whose tags are being edited.</summary>
    [Parameter, EditorRequired]
    public OsmElementRef ElementRef { get; set; } = null!;

    // Hierarchy node types used by the template.
    private record HierarchyLeaf(string Label, string FullKey);
    private record HierarchyL2(string Label, List<HierarchyLeaf> Leaves);
    private record HierarchyL1(string Label, List<HierarchyL2> Children);

    // TODO: Use NSI Schema 'dist/fields.json'
    private bool IsHierarchical
        => Field.Type == "address"
            || (Field.Type == "multiCombo"
                && Field.Key.EndsWith(':')
                && Field.Options.Count > 0);

    private List<HierarchyL1> GetKeyHierarchy()
    {
        IEnumerable<string> fullKeys = Field.Type == "address"
            ? Field.Keys
            : Field.Options.Select(opt => Field.Key + opt);

        return [.. fullKeys.Select(k => (Segments: k.Split(':', 3), FullKey: k))
                           .GroupBy(x => x.Segments[0])
                           .Select(l1 => new HierarchyL1(l1.Key,
                               [.. l1.GroupBy(x => x.Segments.Length > 2 ? x.Segments[1] : string.Empty)
                               .Select(l2 => new HierarchyL2(l2.Key,
                                   [.. l2.Select(x => new HierarchyLeaf(x.Segments[^1], x.FullKey))]))]))];
    }

    private string GetTagValue(string key)
    {
        IReadOnlyDictionary<string, string>? tags = GetTags();
        return tags is not null && tags.TryGetValue(key, out string? v) ? v : string.Empty;
    }

    private async Task SetTagValue(string key, string newValue)
    {
        IReadOnlyDictionary<string, string>? tags = GetTags();
        if (tags is null) return;
        Dictionary<string, string> updated = new(tags) { [key] = newValue };
        await mediator.Send(new UpdateTags.Command(ElementRef, tags, updated));
    }

    private string GetValue()
    {
        IReadOnlyDictionary<string, string>? tags = GetTags();
        return tags is not null && tags.TryGetValue(Field.Key, out string? v) ? v : string.Empty;
    }

    private async Task SetCheckValue(string newValue)
    {
        IReadOnlyDictionary<string, string>? tags = GetTags();
        if (tags is null) return;
        if (string.IsNullOrEmpty(newValue))
        {
            Dictionary<string, string> updated = new(tags);
            updated.Remove(Field.Key);
            await mediator.Send(new UpdateTags.Command(ElementRef, tags, updated));
        }
        else
        {
            await SetValue(newValue);
        }
    }

    private async Task SetValue(string newValue)
    {
        IReadOnlyDictionary<string, string>? tags = GetTags();
        if (tags is null)
        {
            return;
        }

        Dictionary<string, string> updated = new(tags) { [Field.Key] = newValue };
        await mediator.Send(new UpdateTags.Command(ElementRef, tags, updated));
    }

    private IReadOnlyDictionary<string, string>? GetTags()
    {
        EditBufferState buf = editBufferState.State;
        return ElementRef.Type switch
        {
            OsmElementTypes.Node when buf.Nodes.TryGetValue(ElementRef.Id, out OsmNode? n) => n.Tags,
            OsmElementTypes.Way when buf.Ways.TryGetValue(ElementRef.Id, out OsmWay? w) => w.Tags,
            OsmElementTypes.Relation when buf.Relations.TryGetValue(ElementRef.Id, out OsmRelation? r) => r.Tags,
            _ => null
        };
    }
}
