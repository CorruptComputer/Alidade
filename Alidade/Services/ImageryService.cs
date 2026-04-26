using Alidade.Osm.ImageryLayers;
using Alidade.Osm.Models.Imagery;
using NetTopologySuite.Geometries;

namespace Alidade.Services;

/// <summary>
///   Provides location-aware filtering of TMS imagery sources so the background panel
///   can surface only imagery sources that cover the current map viewport.
///   Data is sourced from the generated <see cref="ImageryData"/>.
/// </summary>
/// <param name="log">Logger for reporting geometry building errors.</param>
/// <param name="factory">The WGS 84 geometry factory used to construct NTS geometries.</param>
public sealed class ImageryService(ILogger<ImageryService> log, GeometryFactory factory)
{
    private IReadOnlyList<(ImageryEntry Entry, Geometry? Coverage)> _indexed = [];
    private bool _loaded;

    /// <summary>Error message set when coverage geometry building fails, or null.</summary>
    public string? LoadError { get; private set; }

    /// <summary>
    ///   Builds NTS coverage geometries for location-aware filtering on first call,
    ///   subsequent calls return immediately.
    /// </summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        try
        {
            const int ChunkSize = 50;
            int geomErrors = 0;
            List<(ImageryEntry, Geometry?)> indexed = new(ImageryData.All.Count);

            for (int i = 0; i < ImageryData.All.Count; i += ChunkSize)
            {
                int end = Math.Min(i + ChunkSize, ImageryData.All.Count);
                for (int j = i; j < end; j++)
                {
                    ImageryEntry e = ImageryData.All[j];
                    Geometry? geom = null;
                    try
                    {
                        geom = e.BuildCoverageGeometry(factory);
                    }
                    catch (Exception gex)
                    {
                        geomErrors++;
                        log.LogWarning("ImageryService: geometry error for {Id}: {Message}", e.Id, gex.Message);
                    }

                    indexed.Add((e, geom));
                }
            }

            _indexed = indexed;
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            log.LogError(ex, "ImageryService: failed to build coverage geometries");
        }
        finally
        {
            _loaded = true;
        }
    }

    /// <summary>
    ///   Returns imagery entries relevant to the given map centre point.
    ///   Worldwide entries (no coverage polygon) are always included.
    ///   Region-specific entries are included only when the centre falls inside
    ///   their coverage polygon.
    /// </summary>
    public IReadOnlyList<ImageryEntry> GetForLocation(Coordinate location)
    {
        Point centre = factory.CreatePoint(location);

        return [.. _indexed
            .Where(t => t.Coverage is null || t.Coverage.Contains(centre))
            .Select(t => t.Entry)
            .OrderByDescending(e => e.Best)
            .ThenBy(e => e.Name)];
    }

    /// <summary>
    ///   Searches the full entry list by name for use in the search box.
    ///   Returns up to <paramref name="limit"/> results, with <c>best</c> entries ranked first.
    /// </summary>
    public IReadOnlyList<ImageryEntry> Search(string query, int limit = 50)
    {
        string q = query.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(q))
        {
            return [];
        }

        return [.. ImageryData.All
            .Where(e => e.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Best)
            .ThenBy(e => e.Name)
            .Take(limit)];
    }

    /// <summary>
    ///   All usable imagery entries, regardless of location.
    /// </summary>
    public IReadOnlyList<ImageryEntry> All => ImageryData.All;

    /// <summary>
    ///   Whether the imagery service has finished building coverage geometries.
    /// </summary>
    public bool IsLoaded => _loaded;
}
