namespace OpResult;

/// <summary>
/// Provides extension methods for composing OpError values.
/// </summary>
public static class OpErrorExtensions
{
    /// <summary>
    /// Wraps this error as the direct inner error of a new outer error.
    /// </summary>
    /// <param name="innerError">The direct inner error that caused the new error.</param>
    /// <param name="message">The outer error message.</param>
    /// <returns>A new outer error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="innerError"/> is <see langword="null"/>.</exception>
    public static OpError ToErr(this OpError innerError, string? message)
    {
        ArgumentNullException.ThrowIfNull(innerError);

        return OpResults.Err(message, innerError);
    }
}