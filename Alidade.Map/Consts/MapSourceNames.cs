namespace Alidade.Map.Consts;

/// <summary>
///   MapLibre GeoJSON source IDs used by the C# interop layer.
///   All values here must match the names registered in <c>addOsmSources()</c> in
///   <c>map-interop.js</c>.
/// </summary>
public static class MapSourceNames
{
    /// <summary>
    ///   The source for OSM node point features.
    /// </summary>
    public const string Nodes = "osm-nodes";

    /// <summary>
    ///   The source for OSM way line and area features.
    /// </summary>
    public const string Ways = "osm-ways";

    /// <summary>
    ///   The source for OSM relation features.
    /// </summary>
    public const string Relations = "osm-relations";

    /// <summary>
    ///   The source for the selected-element highlight overlay.
    /// </summary>
    public const string Selected = "osm-selected";

    /// <summary>
    ///   The source for the hovered-element highlight overlay.
    /// </summary>
    public const string Hover = "osm-hover";

    /// <summary>
    ///   The source for way-vertex dot markers shown during editing.
    /// </summary>
    public const string Vertices = "osm-vertices";

    /// <summary>
    ///   The source for OSM note markers.
    /// </summary>
    public const string Notes = "osm-notes";

    /// <summary>
    ///   The source for the GPX track overlay.
    /// </summary>
    public const string Gpx = "osm-gpx";

    /// <summary>
    ///   The source for the draw-tool rubber-band geometry preview.
    /// </summary>
    public const string DrawPreview = "osm-draw-preview";

    /// <summary>
    ///   The source for tool geometry previews (circularize, gridify, etc.).
    /// </summary>
    public const string Preview = "osm-preview";

    /// <summary>
    ///   The source for validation-error highlight overlays.
    /// </summary>
    public const string Invalid = "osm-invalid";

    /// <summary>
    ///   The source for snap-segment hit targets populated during a node drag.
    /// </summary>
    public const string SnapSegments = "osm-snap-segments";
}
