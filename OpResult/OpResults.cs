namespace OpResult;

/// <summary>
/// Provides factory methods for creating successful results and errors.
/// </summary>
public static class OpResults
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="value">The value carried by the result.</param>
    /// <returns>A successful result.</returns>
    public static OpResult<T> Ok<T>(T value) => OpResult<T>.Ok(value);

    /// <summary>
    /// Creates an error.
    /// </summary>
    /// <param name="message">The error message. Null or whitespace messages are normalized to an empty string at run time.</param>
    /// <returns>An error.</returns>
    public static OpError Err(string message) => OpError.New(message);
}
