using System.Diagnostics.CodeAnalysis;

namespace OpResult;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with an error.
/// </summary>
/// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
public readonly record struct OpResult<T>
{
    private readonly bool _isOk;
    private readonly T? _value;
    private readonly OpError? _error;

    private OpResult(bool isOk, T? value, OpError? error)
    {
        _isOk = isOk;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the result represents a success.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsOk => _isOk;

    /// <summary>
    /// Gets a value indicating whether the result represents a failure.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsErr => !_isOk;

    /// <summary>
    /// Gets the value carried by a successful result, or the default value of <typeparamref name="T"/> for a failed result.
    /// </summary>
    public T? Value => _value;

    /// <summary>
    /// Gets the error carried by a failed result, or an empty error for a successful result.
    /// </summary>
    public OpError? Error => _error ?? OpError.Empty;

    internal static OpResult<T> Ok(T? value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, null);
    }

    internal static OpResult<T> Err(OpError error) => new(false, default, error);

    /// <summary>
    /// Converts a value to a successful result.
    /// </summary>
    /// <param name="value">The value carried by the result.</param>
    /// <returns>A successful result.</returns>
    public static implicit operator OpResult<T>(T value) => Ok(value);

    /// <summary>
    /// Converts an error to a failed result.
    /// </summary>
    /// <param name="error">The error carried by the result.</param>
    /// <returns>A failed result.</returns>
    public static implicit operator OpResult<T>(OpError error) => Err(error);
}
