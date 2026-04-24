using Alidade.OsmGen.Generators;

namespace Alidade.OsmGen;

/// <summary>
///   Generates static C# files from OSM data packages for use in Alidade.Osm.
///   This includes the tagging schema, name suggestion index, and imagery layer definitions.
/// </summary>
public static class Program
{
    /// <summary>
    ///   Alidade.OsmGen entry point.
    /// </summary>
    public static async Task Main()
    {
        // Resolve paths relative to the OsmGen project root so the generator works
        // regardless of the working directory when invoked.
        string genRoot = AppContext.BaseDirectory;
        string projectRoot = Path.GetFullPath(Path.Combine(genRoot, "..", "..", ".."));
        string nodeModulesDir = Path.Combine(projectRoot, "node_modules");
        string osmRoot = Path.GetFullPath(Path.Combine(projectRoot, "..", "Alidade.Osm"));

        Console.WriteLine("Alidade - OSM Generator");
        Console.WriteLine($"  OsmGen root  : {projectRoot}");
        Console.WriteLine($"  node_modules : {nodeModulesDir}");
        Console.WriteLine($"  Osm root  : {osmRoot}");
        Console.WriteLine();

        if (!Directory.Exists(nodeModulesDir))
        {
            Console.Error.WriteLine("ERROR: node_modules not found. Run `npm install` first.");
            return;
        }

        if (!Directory.Exists(osmRoot))
        {
            Console.Error.WriteLine($"ERROR: Alidade.Osm not found at {osmRoot}");
            return;
        }

        string taggingOut = Path.Combine(osmRoot, "AutoGen", "TaggingSchemas");
        string nsiOut = Path.Combine(osmRoot, "AutoGen", "NameSuggestions");
        string imageryOut = Path.Combine(osmRoot, "AutoGen", "ImageryLayers");

        Directory.CreateDirectory(taggingOut);
        Directory.CreateDirectory(nsiOut);
        Directory.CreateDirectory(imageryOut);

        // Run generators
        Console.WriteLine("=== Tagging Schema ===");
        TaggingSchemaGenerator.Generate(nodeModulesDir, taggingOut);

        Console.WriteLine();
        Console.WriteLine("=== Name Suggestion Index ===");
        NsiGenerator.Generate(nodeModulesDir, nsiOut);

        Console.WriteLine();
        Console.WriteLine("=== Imagery Layers ===");
        ImageryGenerator.Generate(nodeModulesDir, imageryOut);

        Console.WriteLine();
        Console.WriteLine("Done. Build Alidade.Osm to pick up the generated files.");
    }
}
