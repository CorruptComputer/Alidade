namespace Alidade.Components.Panels;

/// <summary>
///   Implemented by panel component classes to supply chrome metadata to <see cref="Panel{TPanel}"/>.
/// </summary>
public interface IPanel
{
    /// <summary>
    ///   Default title shown in the panel header. Override at the instance level via
    ///   <see cref="Panel{TPanel}.Title"/> for panels with dynamic titles.
    /// </summary>
    static abstract string Title { get; }
}
