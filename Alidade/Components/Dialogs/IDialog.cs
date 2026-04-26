namespace Alidade.Components.Dialogs;

/// <summary>
///   Implemented by dialog component classes to supply chrome metadata to <see cref="Dialog{TDialog}"/>.
/// </summary>
public interface IDialog
{
    /// <summary>
    ///   Default title shown in the dialog header. Override at the instance level via
    ///   <see cref="Dialog{TDialog}.Title"/> for dialogs with dynamic titles.
    /// </summary>
    static abstract string Title { get; }
}
