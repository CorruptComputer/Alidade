namespace Alidade.Osm.Models;

/// <summary>
///   Tracks the local edit status of an element in the edit buffer relative to the server.
/// </summary>
public enum EditState
{
    /// <summary>
    ///   The element was fetched from the server and has not been modified locally.
    /// </summary>
    Fetched,

    /// <summary>
    ///   The element was created locally and has not been uploaded yet.
    /// </summary>
    Created,

    /// <summary>
    ///   The element was fetched from the server and has been modified locally.
    /// </summary>
    Modified,

    /// <summary>
    ///   The element has been marked for deletion and will be removed on next upload.
    /// </summary>
    Deleted
}
