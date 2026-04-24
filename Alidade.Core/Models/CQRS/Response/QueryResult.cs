using System.Text.Json.Serialization;

namespace Alidade.Core.Models.CQRS.Response;

/// <summary>
///   Result from a CQRS query.
/// </summary>
/// <typeparam name="TResult"></typeparam>
public sealed record QueryResult<TResult> : ResultBase
{
    // Create from Fail() factory method, or via implicit TResult? conversion.
    private QueryResult() { }

    /// <summary>
    ///   If the query was successful, this should have some data in it.
    /// </summary>
    public TResult? Result { get; init; }

#pragma warning disable CA1000 // Do not declare static members on generic types
    /// <summary>
    ///   Creates a failure response, optionally with the reason why it failed.
    /// </summary>
    public static QueryResult<TResult> Fail(string? failureReason = null) => new()
    {
        Success = false,
        FailReason = failureReason
    };
#pragma warning restore CA1000

    /// <summary>
    ///   Translates a QueryResult&lt;TResult&gt; into a TResult?
    /// </summary>
    public static implicit operator TResult?(QueryResult<TResult> response)
    {
        if (!response.Success || response.Result is null)
        {
            return default;
        }

        return response.Result;
    }

    /// <summary>
    ///   Translates a TResult? into a QueryResult&lt;TResult&gt;
    /// </summary>
    public static implicit operator QueryResult<TResult>(TResult? result)
    {
        if (result is null)
        {
            return new() { Success = false, FailReason = "Result is null" };
        }

        return new() { Success = true, Result = result };
    }
}
