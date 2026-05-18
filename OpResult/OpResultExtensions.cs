namespace OpResult;

/// <summary>
/// Provides extension methods for composing and consuming OpResult values.
/// </summary>
public static class OpResultExtensions
{
    /// <summary>
    /// Invokes the next operation when the result is successful; otherwise, propagates the error.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the next result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onOk">The operation to invoke when the result is successful.</param>
    /// <returns>The result returned by <paramref name="onOk"/>, or the propagated error.</returns>
    public static OpResult<TNext> Then<T, TNext>(
        this OpResult<T> result,
        Func<T, OpResult<TNext>> onOk) =>
        result.IsOk ? onOk(result.Value) : result.Error!;

    /// <summary>
    /// Invokes the next asynchronous operation when the result is successful; otherwise, propagates the error.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the next result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onOk">The asynchronous operation to invoke when the result is successful.</param>
    /// <returns>A task that represents the next result or the propagated error.</returns>
    public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this OpResult<T> result,
        Func<T, Task<OpResult<TNext>>> onOk) =>
        result.IsOk ? onOk(result.Value) : Task.FromResult<OpResult<TNext>>(result.Error!);

    /// <summary>
    /// Awaits the result and invokes the next asynchronous operation when it is successful; otherwise, propagates the error.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the next result.</typeparam>
    /// <param name="resultTask">The task that produces the result to evaluate.</param>
    /// <param name="onOk">The asynchronous operation to invoke when the result is successful.</param>
    /// <returns>A task that represents the next result or the propagated error.</returns>
    public static async Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<OpResult<TNext>>> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes an action when the result is successful and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onOk">The action to invoke when the result is successful.</param>
    /// <returns>The original result.</returns>
    public static OpResult<T> OnOk<T>(
        this OpResult<T> result,
        Action<T> onOk)
    {
        if (result.IsOk)
        {
            onOk(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Invokes an asynchronous action when the result is successful and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onOk">The asynchronous action to invoke when the result is successful.</param>
    /// <returns>A task that represents the original result.</returns>
    public static async Task<OpResult<T>> OnOkAsync<T>(
        this OpResult<T> result,
        Func<T, Task> onOk)
    {
        if (result.IsOk)
        {
            await onOk(result.Value).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Awaits the result, invokes an asynchronous action when it is successful, and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="resultTask">The task that produces the result to evaluate.</param>
    /// <param name="onOk">The asynchronous action to invoke when the result is successful.</param>
    /// <returns>A task that represents the original result.</returns>
    public static async Task<OpResult<T>> OnOkAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnOkAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Invokes an action when the result is failed and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onErr">The action to invoke when the result is failed.</param>
    /// <returns>The original result.</returns>
    public static OpResult<T> OnErr<T>(
        this OpResult<T> result,
        Action<OpError> onErr)
    {
        if (result.IsErr)
        {
            onErr(result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Invokes an asynchronous action when the result is failed and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to evaluate.</param>
    /// <param name="onErr">The asynchronous action to invoke when the result is failed.</param>
    /// <returns>A task that represents the original result.</returns>
    public static async Task<OpResult<T>> OnErrAsync<T>(
        this OpResult<T> result,
        Func<OpError, Task> onErr)
    {
        if (result.IsErr)
        {
            await onErr(result.Error!).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Awaits the result, invokes an asynchronous action when it is failed, and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="resultTask">The task that produces the result to evaluate.</param>
    /// <param name="onErr">The asynchronous action to invoke when the result is failed.</param>
    /// <returns>A task that represents the original result.</returns>
    public static async Task<OpResult<T>> OnErrAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnErrAsync(onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches the successful or failed result and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <typeparam name="TResult">The type returned by the matching branch.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The function to invoke when the result is successful.</param>
    /// <param name="onErr">The function to invoke when the result is failed.</param>
    /// <returns>The value returned by the matching branch.</returns>
    public static TResult Match<T, TResult>(
        this OpResult<T> result,
        Func<T, TResult> onOk,
        Func<OpError, TResult> onErr) =>
        result.IsOk ? onOk(result.Value) : onErr(result.Error!);

    /// <summary>
    /// Matches the successful or failed result and invokes the corresponding action.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The action to invoke when the result is successful.</param>
    /// <param name="onErr">The action to invoke when the result is failed.</param>
    public static void Match<T>(
        this OpResult<T> result,
        Action<T> onOk,
        Action<OpError> onErr)
    {
        if (result.IsOk)
        {
            onOk(result.Value);
            return;
        }

        onErr(result.Error!);
    }

    /// <summary>
    /// Asynchronously matches the successful or failed result and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <typeparam name="TResult">The type returned by the matching branch.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous function to invoke when the result is successful.</param>
    /// <param name="onErr">The asynchronous function to invoke when the result is failed.</param>
    /// <returns>A task that represents the value returned by the matching branch.</returns>
    public static Task<TResult> MatchAsync<T, TResult>(
        this OpResult<T> result,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr) =>
        result.IsOk ? onOk(result.Value) : onErr(result.Error!);

    /// <summary>
    /// Awaits the result, asynchronously matches the successful or failed result, and returns a value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <typeparam name="TResult">The type returned by the matching branch.</typeparam>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous function to invoke when the result is successful.</param>
    /// <param name="onErr">The asynchronous function to invoke when the result is failed.</param>
    /// <returns>A task that represents the value returned by the matching branch.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Asynchronously matches the successful or failed result and invokes the corresponding action.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous action to invoke when the result is successful.</param>
    /// <param name="onErr">The asynchronous action to invoke when the result is failed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static Task MatchAsync<T>(
        this OpResult<T> result,
        Func<T, Task> onOk,
        Func<OpError, Task> onErr) =>
        result.IsOk ? onOk(result.Value) : onErr(result.Error!);

    /// <summary>
    /// Awaits the result, asynchronously matches the successful or failed result, and invokes the corresponding action.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the result.</typeparam>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous action to invoke when the result is successful.</param>
    /// <param name="onErr">The asynchronous action to invoke when the result is failed.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task MatchAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task> onOk,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }
}
