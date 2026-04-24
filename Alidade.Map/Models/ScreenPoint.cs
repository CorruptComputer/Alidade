namespace Alidade.Map.Models;

/// <summary>
///   A pixel coordinate on the map canvas.
/// </summary>
/// <param name="X">Horizontal offset in CSS pixels from the left edge of the map container.</param>
/// <param name="Y">Vertical offset in CSS pixels from the top edge of the map container.</param>
public record ScreenPoint(double X, double Y);
