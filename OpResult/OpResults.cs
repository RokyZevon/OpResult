using System.Diagnostics.CodeAnalysis;

namespace OpResult;

/// <summary>
/// Provides factory methods for creating successful and failed results.
/// </summary>
public static class OpResults
{
    /// <summary>
    /// Creates a successful result without a value.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static OpResult Ok() => OpResult.Ok();

    /// <summary>
    /// Creates a successful result with a value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="value">The value carried by the result.</param>
    /// <returns>A successful result.</returns>
    public static OpResult<T> Ok<T>([DisallowNull] T? value)
        where T : notnull =>
        OpResult<T>.Ok(value);

    /// <summary>
    /// Creates a failed result without a value.
    /// </summary>
    /// <param name="message">The error message. <see langword="null"/> or whitespace messages are normalized to an empty string at run time.</param>
    /// <returns>A failed result.</returns>
    public static OpResult Err(string? message) => OpResult.Err(OpError.New(message));

    /// <summary>
    /// Creates a failed result with success value type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="message">The error message. <see langword="null"/> or whitespace messages are normalized to an empty string at run time.</param>
    /// <returns>A failed result.</returns>
    public static OpResult<T> Err<T>(string? message)
        where T : notnull =>
        OpResult<T>.Err(OpError.New(message));
}
