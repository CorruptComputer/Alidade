namespace Alidade.Osm.Models.Validation;

/// <summary>
///   How serious a validation issue is; controls icon, colour, and sort order in the panel.
/// </summary>
public enum ValidationSeverities
{
    /// <summary>
    ///   A blocking problem that will prevent or corrupt an OSM upload.
    /// </summary>
    Error,

    /// <summary>
    ///   A likely problem that should be reviewed before uploading.
    /// </summary>
    Warning,

    /// <summary>
    ///   An informational notice that may or may not require action.
    /// </summary>
    Info
}
