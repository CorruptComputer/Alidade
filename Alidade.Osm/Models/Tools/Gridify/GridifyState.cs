namespace Alidade.Osm.Models.Tools.Gridify;

/// <summary>
///   Captures the current configuration for a gridify operation.
/// </summary>
public sealed record GridifyState
{
    /// <summary>
    ///   The ID of the closed way to split, or <see langword="null"/> if none is selected.
    /// </summary>
    public long? WayId { get; init; }

    /// <summary>
    ///   Number of rows in the output grid.
    /// </summary>
    public int Rows { get; init; } = 1;

    /// <summary>
    ///   Extension angle of rows in degrees (direction each row cell runs).
    /// </summary>
    public double RowRotationDeg { get; init; }

    /// <summary>
    ///   Row corner-rounding radius in degrees (reserved, no effect yet).
    /// </summary>
    public double RowRadiusDeg { get; init; }

    /// <summary>
    ///   Number of columns in the output grid.
    /// </summary>
    public int Cols { get; init; } = 1;

    /// <summary>
    ///   Extension angle of columns in degrees (direction each column cell runs).
    /// </summary>
    public double ColRotationDeg { get; init; }

    /// <summary>
    ///   Column corner-rounding radius in degrees (reserved, no effect yet).
    /// </summary>
    public double ColRadiusDeg { get; init; }
}
