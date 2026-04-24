using System.Text.Json.Serialization;

namespace Alidade.Core.Models.CQRS.Response;

/// <summary>
///   Basic response of any request.
/// </summary>
public record ResultBase
{
    /// <summary>
    ///   Base constructor for this response base
    /// </summary>
    protected ResultBase() { }

    /// <summary>
    ///   Was the command successful?
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    ///   Optional even if the command failed, but maybe the reason why it failed.
    /// </summary>
    public string? FailReason { get; init; }
}
