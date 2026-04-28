namespace Alidade.Osm.Models.Tools.Circularize;

/// <summary>
///   Holds the active selection and configuration for the circularize panel.
/// </summary>
public sealed record CircularizeState
{
    /// <summary>
    ///   Gets the ID of the way being circularized, or <see langword="null"/> when no valid
    ///   closed way is selected.
    /// </summary>
    public long? WayId { get; init; }

    /// <summary>
    ///   Gets the target vertex count for the output circle.
    /// </summary>
    public int VertexCount { get; init; } = 16;
}
