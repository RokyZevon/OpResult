using System.Diagnostics.CodeAnalysis;

namespace OpResult;

/// <summary>
/// Rust-style Result type with two generic parameters: T (success value) and E (error value).
/// E has no constraints - it can be string, int, custom type, or any type.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <typeparam name="E">The type of the error value.</typeparam>
public readonly record struct OpResult<T, E>
{
    private readonly bool _isOk;
    private readonly T _value;
    private readonly E _error;

    private OpResult(bool isOk, T value, E error)
    {
        _isOk = isOk;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether this is an Ok result.
    /// </summary>
    public bool IsOk => _isOk;

    /// <summary>
    /// Gets a value indicating whether this is an Err result (including default instances).
    /// </summary>
    public bool IsErr => !_isOk;

    /// <summary>
    /// Creates an Ok result with the specified value.
    /// </summary>
#pragma warning disable CS8604 // Per spec: inactive error field in Ok may be default(E), which may be null
    public static OpResult<T, E> Ok(T value) => new(true, value, default(E));
#pragma warning restore CS8604

    /// <summary>
    /// Creates an Err result with the specified error.
    /// </summary>
#pragma warning disable CS8604 // Per spec: inactive value field in Err may be default(T), which may be null
    public static OpResult<T, E> Err(E error) => new(false, default(T), error);
#pragma warning restore CS8604

    /// <summary>
    /// Implicitly converts a value to an Ok result.
    /// </summary>
    public static implicit operator OpResult<T, E>(T value) => Ok(value);

    /// <summary>
    /// Implicitly converts an error to an Err result.
    /// </summary>
    public static implicit operator OpResult<T, E>(E error) => Err(error);

    /// <summary>
    /// Matches the result and returns a value based on whether it's Ok or Err.
    /// </summary>
    public TOut Match<TOut>([DisallowNull] Func<T, TOut>? onOk, [DisallowNull] Func<E, TOut>? onErr)
    {
        if (onOk is null || onErr is null)
        {
#pragma warning disable CS8603 // Intentionally returning default(TOut) when delegates are null
            return default;
#pragma warning restore CS8603
        }

        return _isOk ? onOk(_value) : onErr(_error);
    }

    /// <summary>
    /// Matches the result and executes an action based on whether it's Ok or Err.
    /// </summary>
    public void Match([DisallowNull] Action<T>? onOk, [DisallowNull] Action<E>? onErr)
    {
        if (onOk is null || onErr is null)
            return;

        if (_isOk)
            onOk(_value);
        else
            onErr(_error);
    }

    /// <summary>
    /// Transforms the Ok value using the provided function. Err values are passed through unchanged.
    /// </summary>
    public OpResult<U, E> Map<U>([DisallowNull] Func<T, U>? map)
    {
        if (!_isOk)
            return OpResult<U, E>.Err(_error);

        if (map is null)
#pragma warning disable CS8604 // Per spec: null delegate returns Err with default(E), which may be null
            return OpResult<U, E>.Err(default(E));
#pragma warning restore CS8604

        return OpResult<U, E>.Ok(map(_value));
    }

    /// <summary>
    /// Transforms the Err value using the provided function. Ok values are passed through unchanged.
    /// </summary>
    public OpResult<T, F> MapErr<F>([DisallowNull] Func<E, F>? map)
    {
        if (_isOk)
            return OpResult<T, F>.Ok(_value);

        if (map is null)
#pragma warning disable CS8604 // Per spec: null delegate returns Err with default(F), which may be null
            return OpResult<T, F>.Err(default(F));
#pragma warning restore CS8604

        return OpResult<T, F>.Err(map(_error));
    }

    /// <summary>
    /// Chains fallible operations (flatMap/bind).
    /// </summary>
    public OpResult<U, E> AndThen<U>([DisallowNull] Func<T, OpResult<U, E>>? bind)
    {
        if (!_isOk)
            return OpResult<U, E>.Err(_error);

        if (bind is null)
#pragma warning disable CS8604 // Per spec: null delegate returns Err with default(E), which may be null
            return OpResult<U, E>.Err(default(E));
#pragma warning restore CS8604

        return bind(_value);
    }

    /// <summary>
    /// Tries to get the Ok value.
    /// </summary>
    /// <returns>true if Ok, false if Err.</returns>
    public bool TryGetValue([MaybeNull] out T value)
    {
        if (_isOk)
        {
            value = _value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Tries to get the Err value.
    /// </summary>
    /// <returns>true if Err, false if Ok.</returns>
    public bool TryGetError([MaybeNull] out E error)
    {
        if (!_isOk)
        {
            error = _error;
            return true;
        }
        error = default;
        return false;
    }
}

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
        return new OpResult<U>(_inner.Map(map));
    }

    /// <summary>
    /// Transforms the Err value using the provided function.
    /// </summary>
    public OpResult<T> MapErr([DisallowNull] Func<OpError, OpError>? map)
    {
        return new OpResult<T>(_inner.MapErr(map));
    }

    /// <summary>
    /// Chains fallible operations.
    /// </summary>
    public OpResult<U> AndThen<U>([DisallowNull] Func<T, OpResult<U>>? bind)
    {
        // Avoid allocating a closure - handle null delegate case directly
        if (bind is null)
        {
#pragma warning disable CS8625 // Null literal is intentionally passed to inner AndThen per spec
#pragma warning disable CS8604 // Null delegate is intentionally passed to inner AndThen per spec
            return new OpResult<U>(_inner.AndThen<U>(null));
#pragma warning restore CS8604
#pragma warning restore CS8625
        }

        // Manually chain without allocating: check inner result and call bind if Ok
        if (!_inner.TryGetValue(out var value))
        {
            _inner.TryGetError(out var error);
            return OpResult<U>.Err(error);
        }

#pragma warning disable CS8604 // value may be null per [MaybeNull] but bind handles it
        var outerResult = bind(value);
#pragma warning restore CS8604
        if (outerResult.TryGetValue(out var innerValue))
        {
#pragma warning disable CS8604 // innerValue may be null per [MaybeNull] but Ok handles it
            return OpResult<U>.Ok(innerValue);
#pragma warning restore CS8604
        }

        outerResult.TryGetError(out var innerError);
        return OpResult<U>.Err(innerError);
    }

    /// <summary>
    /// Tries to get the Ok value.
    /// </summary>
    public bool TryGetValue([MaybeNull] out T value)
        => _inner.TryGetValue(out value);

    /// <summary>
    /// Tries to get the Err value.
    /// </summary>
    public bool TryGetError([MaybeNull] out OpError error)
        => _inner.TryGetError(out error);
}
