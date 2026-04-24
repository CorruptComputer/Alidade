using System.Text.Json.Serialization;

namespace Alidade.Core.Models.CQRS.Response;

/// <summary>
///   Basic response of any CQRS command.
/// </summary>
public sealed record CommandResult : ResultBase
{
    // Create from Pass() or Fail() factory methods, or via implicit bool conversion.
    private CommandResult() { }

    /// <summary>
    ///   Creates a successful response.
    /// </summary>
    public static CommandResult Pass() => new()
    {
        Success = true
    };

    /// <summary>
    ///   Creates a failure response, optionally with the reason why it failed.
    /// </summary>
    public static CommandResult Fail(string? failureReason = null) => new()
    {
        Success = false,
        FailReason = failureReason
    };

    /// <summary>
    ///   Translates a bool into a CommandResult, assuming true means the command was successful.
    /// </summary>
    public static implicit operator CommandResult(bool success)
        => new() { Success = success };

    /// <summary>
    ///   Translates a CommandResult into a bool, assuming true means the command was successful.
    /// </summary>
    public static implicit operator bool(CommandResult response)
        => response.Success;
}
