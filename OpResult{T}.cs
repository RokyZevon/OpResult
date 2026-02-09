using System.Diagnostics.CodeAnalysis;

namespace OpResult;

/// <summary>
/// Convenience wrapper for OpResult&lt;T, OpError&gt; with the same API surface.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
public readonly record struct OpResult<T>
{
    private readonly OpResult<T, OpError> _inner;

    private OpResult(OpResult<T, OpError> inner)
    {
        _inner = inner;
    }

    /// <summary>
    /// Gets a value indicating whether this is an Ok result.
    /// </summary>
    public bool IsOk => _inner.IsOk;

    /// <summary>
    /// Gets a value indicating whether this is an Err result.
    /// </summary>
    public bool IsErr => _inner.IsErr;

    /// <summary>
    /// Creates an Ok result with the specified value.
    /// </summary>
    public static OpResult<T> Ok(T value) => new(OpResult<T, OpError>.Ok(value));

    /// <summary>
    /// Creates an Err result with the specified error.
    /// </summary>
    public static OpResult<T> Err(OpError error) => new(OpResult<T, OpError>.Err(error));

    /// <summary>
    /// Creates an Err result with the specified message.
    /// </summary>
    public static OpResult<T> Err(string message) => new(OpResult<T, OpError>.Err(OpError.New(message)));

    /// <summary>
    /// Creates an Err result with the specified code and message.
    /// </summary>
    public static OpResult<T> Err(string code, string message) => new(OpResult<T, OpError>.Err(OpError.New(code, message)));

    /// <summary>
    /// Implicitly converts a value to an Ok result.
    /// </summary>
    public static implicit operator OpResult<T>(T value) => Ok(value);

    /// <summary>
    /// Implicitly converts an OpError to an Err result.
    /// </summary>
    public static implicit operator OpResult<T>(OpError error) => Err(error);

    /// <summary>
    /// Matches the result and returns a value based on whether it's Ok or Err.
    /// </summary>
    public TOut Match<TOut>([DisallowNull] Func<T, TOut>? onOk, [DisallowNull] Func<OpError, TOut>? onErr)
        => _inner.Match(onOk, onErr);

    /// <summary>
    /// Matches the result and executes an action based on whether it's Ok or Err.
    /// </summary>
    public void Match([DisallowNull] Action<T>? onOk, [DisallowNull] Action<OpError>? onErr)
        => _inner.Match(onOk, onErr);

    /// <summary>
    /// Transforms the Ok value using the provided function.
    /// </summary>
    public OpResult<U> Map<U>([DisallowNull] Func<T, U>? map)
    {
        var result = _inner.Map(map);
        if (result.TryGetValue(out var value))
            return OpResult<U>.Ok(value);
        
        result.TryGetError(out var error);
        return OpResult<U>.Err(error!);
    }

    /// <summary>
    /// Transforms the Err value using the provided function.
    /// </summary>
    public OpResult<T> MapErr([DisallowNull] Func<OpError, OpError>? map)
    {
        var result = _inner.MapErr(map);
        if (result.TryGetValue(out var value))
            return OpResult<T>.Ok(value);
        
        result.TryGetError(out var error);
        return OpResult<T>.Err(error!);
    }

    /// <summary>
    /// Chains fallible operations.
    /// </summary>
    public OpResult<U> AndThen<U>([DisallowNull] Func<T, OpResult<U>>? bind)
    {
        if (bind is null)
            return OpResult<U>.Err(OpError.New("Bind function is null"));

        if (_inner.TryGetError(out var error))
            return OpResult<U>.Err(error);

        if (!_inner.TryGetValue(out var value))
            return OpResult<U>.Err(OpError.New("Invalid state"));

        return bind(value);
    }

    /// <summary>
    /// Tries to get the Ok value.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
        => _inner.TryGetValue(out value);

    /// <summary>
    /// Tries to get the Err value.
    /// </summary>
    public bool TryGetError([MaybeNullWhen(false)] out OpError error)
        => _inner.TryGetError(out error);
}
