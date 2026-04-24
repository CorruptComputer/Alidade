namespace Alidade.Models.Tool;

/// <summary>
///   Identifies the currently active editing mode.
/// </summary>
public enum ActiveTools
{
    /// <summary>
    ///   The default selection/drag mode.
    /// </summary>
    Select,

    /// <summary>
    ///   Click-to-place individual node mode.
    /// </summary>
    DrawNode,

    /// <summary>
    ///   Click-to-trace open or closed way mode.
    /// </summary>
    DrawWay,

    /// <summary>
    ///   Click-to-trace closed area (adds <c>area=yes</c>) mode.
    /// </summary>
    DrawArea
}
