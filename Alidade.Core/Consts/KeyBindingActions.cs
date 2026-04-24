namespace Alidade.Core.Consts;

/// <summary>
///   String identifiers for every bindable action.
/// </summary>
public static class KeyBindingActions
{
    #region Tools
    /// <summary>
    ///   Activates the Select tool.
    /// </summary>
    public const string SelectTool = "select_tool";

    /// <summary>
    ///   Activates the Add Node tool.
    /// </summary>
    public const string DrawNode = "draw_node";

    /// <summary>
    ///   Activates the Draw Way tool.
    /// </summary>
    public const string DrawWay = "draw_way";

    /// <summary>
    ///   Activates the Draw Area tool.
    /// </summary>
    public const string DrawArea = "draw_area";

    /// <summary>
    ///   Cancels the active drawing tool.
    /// </summary>
    public const string CancelTool = "cancel_tool";
    #endregion

    #region Edit
    /// <summary>
    ///   Undoes the last action.
    /// </summary>
    public const string Undo = "undo";

    /// <summary>
    ///   Redoes the last undone action.
    /// </summary>
    public const string Redo = "redo";

    /// <summary>
    ///   Deletes the currently selected elements.
    /// </summary>
    public const string DeleteSelected = "delete_selected";

    /// <summary>
    ///   Squares the corners of the selected building or closed way.
    /// </summary>
    public const string Orthogonalize = "orthogonalize";

    /// <summary>
    ///   Circularizes the selected closed way.
    /// </summary>
    public const string Circularize = "circularize";

    /// <summary>
    ///   Splits the selected way at the selected node.
    /// </summary>
    public const string SplitWay = "split_way";

    /// <summary>
    ///   Opens the upload dialog to submit pending changes.
    /// </summary>
    public const string Upload = "upload";

    /// <summary>
    ///   Opens the gridify dialog to split a selected closed way into a grid of equal sub-areas.
    /// </summary>
    public const string Gridify = "gridify";
    #endregion

    #region View
    /// <summary>
    ///   Toggles the background imagery panel.
    /// </summary>
    public const string ToggleBackground = "toggle_background";

    /// <summary>
    ///   Toggles the settings panel.
    /// </summary>
    public const string ToggleSettings = "toggle_settings";

    /// <summary>
    ///   Toggles the validation issues panel.
    /// </summary>
    public const string ToggleValidation = "toggle_validation";

    /// <summary>
    ///   Toggles the undo history panel.
    /// </summary>
    public const string ToggleUndoHistory = "toggle_undo_history";

    /// <summary>
    ///   Focuses the preset search input.
    /// </summary>
    public const string FocusSearch = "focus_search";
    #endregion
}
