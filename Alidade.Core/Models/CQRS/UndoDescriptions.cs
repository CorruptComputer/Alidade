namespace Alidade.Core.Models.CQRS;

/// <summary>
///   Maps command types to human-readable undo history labels.
/// </summary>
public static class UndoDescriptions
{
    private static readonly Dictionary<Type, string> Descriptions = [];

    /// <summary>
    ///   Returns the description for a given command type, or its type name as a fallback.
    /// </summary>
    public static string For<T>()
        => Descriptions.TryGetValue(typeof(T), out string? desc) ? desc : typeof(T).Name;

    /// <summary>
    ///   Registers a human-readable label for a command type.
    ///   Call once per undoable command type from a static initializer.
    /// </summary>
    public static void Register<T>(string description)
        => Descriptions[typeof(T)] = description;
}
