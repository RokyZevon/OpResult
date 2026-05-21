namespace OpResult;

/// <summary>
/// Provides extension methods for composing and consuming OpResult values.
/// </summary>
public static class OpResultExtensions
{
    /// <summary>
    /// Continues with a result-producing step after a successful result without a value, and short-circuits on failure.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The continuation to run on success.</param>
    /// <returns>The continuation result, or the original failure error.</returns>
    public static OpResult Then(
        this OpResult result,
        Func<OpResult> onOk) =>
        result.IsOk
            ? onOk()
            : OpResults.Err(result.Error!.Message);

    /// <summary>
    /// Continues with a value-result-producing step after a successful result without a value, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the continuation result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The continuation to run on success.</param>
    /// <returns>The continuation result, or a result carrying the original failure error.</returns>
    public static OpResult<T> Then<T>(
        this OpResult result,
        Func<OpResult<T>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk()
            : OpResults.Err<T>(result.Error!.Message);

    /// <summary>
    /// Continues with a step that maps a successful value to a new value result, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the continuation result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The continuation to run on success.</param>
    /// <returns>The continuation result, or a result carrying the original failure error.</returns>
    public static OpResult<TNext> Then<T, TNext>(
        this OpResult<T> result,
        Func<T, OpResult<TNext>> onOk)
        where T : notnull
        where TNext : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : OpResults.Err<TNext>(result.Error!.Message);

    /// <summary>
    /// Continues with a result-producing step after a successful value result, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The continuation to run on success.</param>
    /// <returns>The continuation result, or the original failure error.</returns>
    public static OpResult Then<T>(
        this OpResult<T> result,
        Func<T, OpResult> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : OpResults.Err(result.Error!.Message);

    /// <summary>
    /// Continues with an asynchronous result-producing step after a successful result without a value, and short-circuits on failure.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static Task<OpResult> ThenAsync(
        this OpResult result,
        Func<Task<OpResult>> onOk) =>
        result.IsOk
            ? onOk()
            : Task.FromResult(OpResults.Err(result.Error!.Message));

    /// <summary>
    /// Continues with an asynchronous value-result-producing step after a successful result without a value, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the continuation result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static Task<OpResult<T>> ThenAsync<T>(
        this OpResult result,
        Func<Task<OpResult<T>>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk()
            : Task.FromResult(OpResults.Err<T>(result.Error!.Message));

    /// <summary>
    /// Continues with an asynchronous step that maps a successful value to a new value result, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the continuation result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this OpResult<T> result,
        Func<T, Task<OpResult<TNext>>> onOk)
        where T : notnull
        where TNext : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : Task.FromResult(OpResults.Err<TNext>(result.Error!.Message));

    /// <summary>
    /// Continues with an asynchronous result-producing step after a successful value result, and short-circuits on failure.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static Task<OpResult> ThenAsync<T>(
        this OpResult<T> result,
        Func<T, Task<OpResult>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : Task.FromResult(OpResults.Err(result.Error!.Message));

    /// <summary>
    /// Awaits a result without a value, then continues with an asynchronous result-producing step on success.
    /// </summary>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static async Task<OpResult> ThenAsync(
        this Task<OpResult> resultTask,
        Func<Task<OpResult>> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a result without a value, then continues with an asynchronous value-result-producing step on success.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the continuation result.</typeparam>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static async Task<OpResult<T>> ThenAsync<T>(
        this Task<OpResult> resultTask,
        Func<Task<OpResult<T>>> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result, then continues with an asynchronous step that maps a successful value to a new value result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <typeparam name="TNext">The type of the value carried by the continuation result.</typeparam>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static async Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<OpResult<TNext>>> onOk)
        where T : notnull
        where TNext : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result, then continues with an asynchronous result-producing step on success.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by the current successful result.</typeparam>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous continuation to run on success.</param>
    /// <returns>A task representing the continuation result or the original failure error.</returns>
    public static async Task<OpResult> ThenAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<OpResult>> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs a side effect on a successful result without a value and returns the original result.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The side effect to run on success.</param>
    /// <returns>The original result.</returns>
    public static OpResult OnOk(
        this OpResult result,
        Action onOk)
    {
        if (result.IsOk)
        {
            onOk();
        }

        return result;
    }

    /// <summary>
    /// Runs a side effect on a successful value result and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The side effect to run on success.</param>
    /// <returns>The original result.</returns>
    public static OpResult<T> OnOk<T>(
        this OpResult<T> result,
        Action<T> onOk)
        where T : notnull
    {
        if (result.IsOk)
        {
            onOk(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Runs a side effect on a failed result without a value and returns the original result.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onErr">The side effect to run on failure.</param>
    /// <returns>The original result.</returns>
    public static OpResult OnErr(
        this OpResult result,
        Action<OpError> onErr)
    {
        if (result.IsErr)
        {
            onErr(result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Runs a side effect on a failed value result and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onErr">The side effect to run on failure.</param>
    /// <returns>The original result.</returns>
    public static OpResult<T> OnErr<T>(
        this OpResult<T> result,
        Action<OpError> onErr)
        where T : notnull
    {
        if (result.IsErr)
        {
            onErr(result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Runs an asynchronous side effect on a successful result without a value and returns the original result.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous side effect to run on success.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult> OnOkAsync(
        this OpResult result,
        Func<Task> onOk)
    {
        if (result.IsOk)
        {
            await onOk().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Runs an asynchronous side effect on a successful value result and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onOk">The asynchronous side effect to run on success.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult<T>> OnOkAsync<T>(
        this OpResult<T> result,
        Func<T, Task> onOk)
        where T : notnull
    {
        if (result.IsOk)
        {
            await onOk(result.Value).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Runs an asynchronous side effect on a failed result without a value and returns the original result.
    /// </summary>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onErr">The asynchronous side effect to run on failure.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult> OnErrAsync(
        this OpResult result,
        Func<OpError, Task> onErr)
    {
        if (result.IsErr)
        {
            await onErr(result.Error!).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Runs an asynchronous side effect on a failed value result and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to inspect.</param>
    /// <param name="onErr">The asynchronous side effect to run on failure.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult<T>> OnErrAsync<T>(
        this OpResult<T> result,
        Func<OpError, Task> onErr)
        where T : notnull
    {
        if (result.IsErr)
        {
            await onErr(result.Error!).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Awaits a result without a value, runs an asynchronous side effect on success, and returns the original result.
    /// </summary>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous side effect to run on success.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult> OnOkAsync(
        this Task<OpResult> resultTask,
        Func<Task> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnOkAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result, runs an asynchronous side effect on success, and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onOk">The asynchronous side effect to run on success.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult<T>> OnOkAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnOkAsync(onOk).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a result without a value, runs an asynchronous side effect on failure, and returns the original result.
    /// </summary>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onErr">The asynchronous side effect to run on failure.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult> OnErrAsync(
        this Task<OpResult> resultTask,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnErrAsync(onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result, runs an asynchronous side effect on failure, and returns the original result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="resultTask">The task that produces the result to inspect.</param>
    /// <param name="onErr">The asynchronous side effect to run on failure.</param>
    /// <returns>A task representing the original result.</returns>
    public static async Task<OpResult<T>> OnErrAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<OpError, Task> onErr)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnErrAsync(onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Matches a result without a value into a return value.
    /// </summary>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The branch function to run on success.</param>
    /// <param name="onErr">The branch function to run on failure.</param>
    /// <returns>The value returned by the selected branch function.</returns>
    public static TResult Match<TResult>(
        this OpResult result,
        Func<TResult> onOk,
        Func<OpError, TResult> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

    /// <summary>
    /// Consumes either branch of a result without a value.
    /// </summary>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The branch action to run on success.</param>
    /// <param name="onErr">The branch action to run on failure.</param>
    public static void Match(
        this OpResult result,
        Action onOk,
        Action<OpError> onErr)
    {
        if (result.IsOk)
        {
            onOk();
            return;
        }

        onErr(result.Error!);
    }

    /// <summary>
    /// Matches a value result into a return value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The branch function to run on success.</param>
    /// <param name="onErr">The branch function to run on failure.</param>
    /// <returns>The value returned by the selected branch function.</returns>
    public static TResult Match<T, TResult>(
        this OpResult<T> result,
        Func<T, TResult> onOk,
        Func<OpError, TResult> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

    /// <summary>
    /// Consumes either branch of a value result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The branch action to run on success.</param>
    /// <param name="onErr">The branch action to run on failure.</param>
    public static void Match<T>(
        this OpResult<T> result,
        Action<T> onOk,
        Action<OpError> onErr)
        where T : notnull
    {
        if (result.IsOk)
        {
            onOk(result.Value);
            return;
        }

        onErr(result.Error!);
    }

    /// <summary>
    /// Matches a result without a value into an asynchronous return value.
    /// </summary>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous branch function to run on success.</param>
    /// <param name="onErr">The asynchronous branch function to run on failure.</param>
    /// <returns>A task representing the value returned by the selected branch function.</returns>
    public static Task<TResult> MatchAsync<TResult>(
        this OpResult result,
        Func<Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

    /// <summary>
    /// Asynchronously consumes either branch of a result without a value.
    /// </summary>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous branch action to run on success.</param>
    /// <param name="onErr">The asynchronous branch action to run on failure.</param>
    /// <returns>A task representing completion of the selected branch action.</returns>
    public static Task MatchAsync(
        this OpResult result,
        Func<Task> onOk,
        Func<OpError, Task> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

    /// <summary>
    /// Matches a value result into an asynchronous return value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous branch function to run on success.</param>
    /// <param name="onErr">The asynchronous branch function to run on failure.</param>
    /// <returns>A task representing the value returned by the selected branch function.</returns>
    public static Task<TResult> MatchAsync<T, TResult>(
        this OpResult<T> result,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

    /// <summary>
    /// Asynchronously consumes either branch of a value result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="result">The result to match.</param>
    /// <param name="onOk">The asynchronous branch action to run on success.</param>
    /// <param name="onErr">The asynchronous branch action to run on failure.</param>
    /// <returns>A task representing completion of the selected branch action.</returns>
    public static Task MatchAsync<T>(
        this OpResult<T> result,
        Func<T, Task> onOk,
        Func<OpError, Task> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

    /// <summary>
    /// Awaits a result without a value and matches it into an asynchronous return value.
    /// </summary>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous branch function to run on success.</param>
    /// <param name="onErr">The asynchronous branch function to run on failure.</param>
    /// <returns>A task representing the value returned by the selected branch function.</returns>
    public static async Task<TResult> MatchAsync<TResult>(
        this Task<OpResult> resultTask,
        Func<Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a result without a value and asynchronously consumes either branch.
    /// </summary>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous branch action to run on success.</param>
    /// <param name="onErr">The asynchronous branch action to run on failure.</param>
    /// <returns>A task representing completion of the selected branch action.</returns>
    public static async Task MatchAsync(
        this Task<OpResult> resultTask,
        Func<Task> onOk,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result and matches it into an asynchronous return value.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <typeparam name="TResult">The type returned by the match expression.</typeparam>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous branch function to run on success.</param>
    /// <param name="onErr">The asynchronous branch function to run on failure.</param>
    /// <returns>A task representing the value returned by the selected branch function.</returns>
    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    /// <summary>
    /// Awaits a value result and asynchronously consumes either branch.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="resultTask">The task that produces the result to match.</param>
    /// <param name="onOk">The asynchronous branch action to run on success.</param>
    /// <param name="onErr">The asynchronous branch action to run on failure.</param>
    /// <returns>A task representing completion of the selected branch action.</returns>
    public static async Task MatchAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task> onOk,
        Func<OpError, Task> onErr)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }
}
