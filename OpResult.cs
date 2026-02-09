namespace OpResult;

/// <summary>
/// Static factory helper class for creating OpResult instances without specifying type parameters.
/// </summary>
public static class OpResult
{
    /// <summary>
    /// Creates an Ok result with the specified value for OpResult&lt;T&gt;.
    /// </summary>
    public static OpResult<T> Ok<T>(T value) => OpResult<T>.Ok(value);

    /// <summary>
    /// Creates an Ok result with the specified value for OpResult&lt;T, E&gt;.
    /// </summary>
    public static OpResult<T, E> Ok<T, E>(T value) => OpResult<T, E>.Ok(value);

    /// <summary>
    /// Creates an Err result with the specified message for OpResult&lt;T&gt;.
    /// </summary>
    public static OpResult<T> Err<T>(string message) => OpResult<T>.Err(message);

    /// <summary>
    /// Creates an Err result with the specified error for OpResult&lt;T, E&gt;.
    /// </summary>
    public static OpResult<T, E> Err<T, E>(E error) => OpResult<T, E>.Err(error);
}
