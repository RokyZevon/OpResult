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
    /// <returns>An error that can be converted to a failed result.</returns>
    public static OpError Err(string? message) => OpError.New(message);

    /// <summary>
    /// Creates a failed result error with a direct inner error.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerError">The direct inner error that caused this error, if any.</param>
    /// <returns>An error that can be converted to a failed result.</returns>
    public static OpError Err(string? message, OpError? innerError) => OpError.New(message, innerError);

    /// <summary>
    /// Creates a failed result with success value type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="message">The error message. <see langword="null"/> or whitespace messages are normalized to an empty string at run time.</param>
    /// <returns>A failed result.</returns>
    public static OpResult<T> Err<T>(string? message)
        where T : notnull =>
        OpResult<T>.Err(OpError.New(message));

    private const string NullOperationResultMessage = "Operation returned null.";

    /// <summary>
    /// Invokes an action and converts non-cancellation exceptions to a failed result.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A successful result when the action completes; otherwise, a failed result with the exception message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The invoked operation throws a cancellation exception.</exception>
    public static OpResult TryInvoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OpResult.Err(MapException(exception));
        }
    }

    /// <summary>
    /// Invokes a function and converts non-cancellation exceptions or null return values to a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <returns>A successful result when the function returns a non-null value; otherwise, a failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The invoked operation throws a cancellation exception.</exception>
    public static OpResult<T> TryInvoke<T>(Func<T> func)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            var value = func();
            return value is null ? Err<T>(NullOperationResultMessage) : Ok(value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OpResult<T>.Err(MapException(exception));
        }
    }

    /// <summary>
    /// Invokes an asynchronous action and converts non-cancellation exceptions or null tasks to a failed result.
    /// </summary>
    /// <param name="action">The asynchronous action to invoke.</param>
    /// <returns>A successful result when the action task completes; otherwise, a failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The invoked operation throws a cancellation exception.</exception>
    public static async Task<OpResult> TryInvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            var task = action();
            if (task is null)
            {
                return Err(NullOperationResultMessage);
            }

            await task.ConfigureAwait(false);
            return Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OpResult.Err(MapException(exception));
        }
    }

    /// <summary>
    /// Invokes an asynchronous function and converts non-cancellation exceptions, null tasks, or null return values to a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="func">The asynchronous function to invoke.</param>
    /// <returns>A successful result when the function task returns a non-null value; otherwise, a failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The invoked operation throws a cancellation exception.</exception>
    public static async Task<OpResult<T>> TryInvokeAsync<T>(Func<Task<T>> func)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(func);

        try
        {
            var task = func();
            if (task is null)
            {
                return Err<T>(NullOperationResultMessage);
            }

            var value = await task.ConfigureAwait(false);
            return value is null ? Err<T>(NullOperationResultMessage) : Ok(value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return OpResult<T>.Err(MapException(exception));
        }
    }

    private static OpError MapException(Exception exception)
    {
        var message = string.IsNullOrEmpty(exception.Message)
            ? exception.GetType().ToString()
            : $"{exception.GetType()}: {exception.Message}";

        var innerError = exception.InnerException is null
            ? null
            : MapException(exception.InnerException);

        return OpError.New(message, innerError);
    }
}