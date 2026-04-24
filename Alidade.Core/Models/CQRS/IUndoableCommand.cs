namespace Alidade.Core.Models.CQRS;

/// <summary>
///   Marker interface. Commands implementing this are intercepted by
///   <c>UndoPipelineBehavior</c> to snapshot the edit buffer before and after execution.
/// </summary>
public interface IUndoableCommand { }
