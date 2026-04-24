using System.Text.RegularExpressions;

namespace Alidade.Osm.Validators.Info;

/// <summary>
///   Provides informational notices when a name tag contains a URL, looks like a phone
///   number, or is written entirely in uppercase, patterns that usually indicate
///   accidental or incorrect data entry.
/// </summary>
public class SuspiciousNameValidator : ValidatorBase
{
    private static readonly Regex _urlPattern =
        new(@"https?://|www\.", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex _phonePattern =
        new(@"^\+?[\d\s\-()]{7,}$", RegexOptions.Compiled);

    /// <inheritdoc />
    public override string ValidatorName => "suspicious_name";

    /// <inheritdoc />
    public override ValidationSeverities Severity => ValidationSeverities.Info;

    /// <inheritdoc />
    public override IEnumerable<ValidationIssue> Check(EditBufferSnapshot snap, PresetService presets)
    {
        foreach (object element in snap.Nodes.Values.Cast<object>()
                                                    .Concat(snap.Ways.Values)
                                                    .Concat(snap.Relations.Values))
        {
            IReadOnlyDictionary<string, string> tags;
            OsmElementRef r;
            long id;
            string type;

            switch (element)
            {
                case OsmNode n:
                    tags = n.Tags; r = n.Ref; id = n.Id; type = "node";
                    break;
                case OsmWay w:
                    tags = w.Tags; r = w.Ref; id = w.Id; type = "way";
                    break;
                case OsmRelation rel:
                    tags = rel.Tags; r = rel.Ref; id = rel.Id; type = "relation";
                    break;
                default:
                    continue;
            }

            if (!tags.TryGetValue("name", out string? name))
            {
                continue;
            }

            if (_urlPattern.IsMatch(name))
            {
                yield return new(Severity, ValidatorName, $"{type} {id}: name tag contains a URL", r, false);
            }
            else if (_phonePattern.IsMatch(name))
            {
                yield return new(Severity, ValidatorName, $"{type} {id}: name tag looks like a phone number", r, false);
            }
            else if (name.Length > 3 && name == name.ToUpperInvariant())
            {
                yield return new(Severity, ValidatorName, $"{type} {id}: name tag is all caps", r, false);
            }
        }
    }
}
