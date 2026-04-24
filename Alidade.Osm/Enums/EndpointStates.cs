namespace Alidade.Osm.Enums;

/// <summary>
///   Controls whether an <see cref="ApiEndpoints"/> entry is available for use.
/// </summary>
public enum EndpointState
{
    /// <summary>
    ///   Full read/write access is available
    /// </summary>
    Enabled,

    /// <summary>
    ///   The endpoint can be browsed but write operations are blocked
    /// </summary>
    ReadOnly,

    /// <summary>
    ///   The endpoint is not available for use
    /// </summary>
    Disabled
}
