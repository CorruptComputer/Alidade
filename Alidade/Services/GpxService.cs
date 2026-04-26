using System.Globalization;
using System.Xml.Linq;
using Alidade.Map.Handlers;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;

namespace Alidade.Services;

/// <summary>
///   Parses <c>.gpx</c> files (tracks, routes, and waypoints) and pushes the combined
///   GeoJSON to the <c>osm-gpx</c> MapLibre source. Multiple files are tracked by
///   filename key; removing a file and re-pushing rebuilds the combined feature collection.
///   Both GPX 1.0 and GPX 1.1 namespace variants are handled automatically.
/// </summary>
/// <remarks>
///   Initializes the service with its mediator and JSON serialization dependencies.
/// </remarks>
/// <param name="mediator">The mediator used to dispatch map source data commands.</param>
/// <param name="geomFactory">The WGS 84 geometry factory used to construct NTS geometries.</param>
public sealed class GpxService(IMediator mediator, GeometryFactory geomFactory)
{
    private readonly Dictionary<string, List<Feature>> _layers = [];

    /// <summary>
    ///   The file names of all currently loaded GPX layers.
    /// </summary>
    public IReadOnlyCollection<string> LoadedFiles
        => _layers.Keys;

    /// <summary>
    ///   Parses a GPX stream and registers its features under <paramref name="filename"/>,
    ///   replacing any previously loaded data for that key. Pushes the updated combined
    ///   feature collection to MapLibre.
    /// </summary>
    /// <param name="filename">
    ///   The display name and dictionary key for this layer (typically the file name).
    /// </param>
    /// <param name="gpxStream">A readable stream containing the GPX XML data.</param>
    public async Task LoadAsync(string filename, Stream gpxStream)
    {
        _layers[filename] = ParseGpx(gpxStream);
        await PushAsync();
    }

    /// <summary>
    ///   Removes the GPX layer identified by <paramref name="filename"/> and pushes the
    ///   updated combined feature collection to MapLibre.
    /// </summary>
    /// <param name="filename">The key of the layer to remove.</param>
    public async Task RemoveAsync(string filename)
    {
        _layers.Remove(filename);
        await PushAsync();
    }

    private async Task PushAsync()
    {
        FeatureCollection fc = [];
        foreach (List<Feature> features in _layers.Values)
        {
            foreach (Feature f in features)
            {
                fc.Add(f);
            }
        }

        await mediator.Send(new SetSourceData.Command("osm-gpx", fc));
    }

    private List<Feature> ParseGpx(Stream stream)
    {
        List<Feature> features = [];
        XDocument doc;

        try
        {
            doc = XDocument.Load(stream);
        }
        catch
        {
            return features;
        }

        XElement? root = doc.Root;
        if (root is null)
        {
            return features;
        }

        // Read the namespace from the document to handle both GPX 1.0 and GPX 1.1.
        XNamespace ns = root.Name.Namespace;

        foreach (XElement trk in root.Elements(ns + "trk"))
        {
            string? name = trk.Element(ns + "name")?.Value;

            foreach (XElement seg in trk.Elements(ns + "trkseg"))
            {
                Coordinate[] pts = [.. seg.Elements(ns + "trkpt")
                    .Select(ParseCoordinate)
                    .Where(c => c is not null)
                    .Cast<Coordinate>()];

                if (pts.Length < 2)
                {
                    continue;
                }

                AttributesTable attrs = [];
                if (name is not null)
                {
                    attrs.Add("name", name);
                }

                attrs.Add("gpx_type", "track");
                features.Add(new Feature(geomFactory.CreateLineString(pts), attrs));
            }
        }

        foreach (XElement rte in root.Elements(ns + "rte"))
        {
            string? name = rte.Element(ns + "name")?.Value;
            Coordinate[] pts = [.. rte.Elements(ns + "rtept")
                .Select(ParseCoordinate)
                .Where(c => c is not null)
                .Cast<Coordinate>()];

            if (pts.Length >= 2)
            {
                AttributesTable attrs = [];
                if (name is not null)
                {
                    attrs.Add("name", name);
                }

                attrs.Add("gpx_type", "route");
                features.Add(new Feature(geomFactory.CreateLineString(pts), attrs));
            }
        }

        foreach (XElement wpt in root.Elements(ns + "wpt"))
        {
            Coordinate? coord = ParseCoordinate(wpt);
            if (coord is null)
            {
                continue;
            }

            AttributesTable attrs = [];
            string? wptName = wpt.Element(ns + "name")?.Value;
            if (wptName is not null)
            {
                attrs.Add("name", wptName);
            }

            attrs.Add("gpx_type", "waypoint");
            features.Add(new Feature(geomFactory.CreatePoint(coord), attrs));
        }

        return features;
    }

    private static Coordinate? ParseCoordinate(XElement el)
    {
        if (!double.TryParse(el.Attribute("lat")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out double lat))
        {
            return null;
        }

        if (!double.TryParse(el.Attribute("lon")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                out double lon))
        {
            return null;
        }

        return new Coordinate(lon, lat);
    }
}
