namespace OpResult;

/// <summary>
/// Provides extension methods for composing and consuming OpResult values.
/// </summary>
public static class OpResultExtensions
{
    public static OpResult Then(
        this OpResult result,
        Func<OpResult> onOk) =>
        result.IsOk
            ? onOk()
            : OpResults.Err(result.Error!.Message);

    public static OpResult<T> Then<T>(
        this OpResult result,
        Func<OpResult<T>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk()
            : OpResults.Err<T>(result.Error!.Message);

    public static OpResult<TNext> Then<T, TNext>(
        this OpResult<T> result,
        Func<T, OpResult<TNext>> onOk)
        where T : notnull
        where TNext : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : OpResults.Err<TNext>(result.Error!.Message);

    public static OpResult Then<T>(
        this OpResult<T> result,
        Func<T, OpResult> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : OpResults.Err(result.Error!.Message);

    public static Task<OpResult> ThenAsync(
        this OpResult result,
        Func<Task<OpResult>> onOk) =>
        result.IsOk
            ? onOk()
            : Task.FromResult(OpResults.Err(result.Error!.Message));

    public static Task<OpResult<T>> ThenAsync<T>(
        this OpResult result,
        Func<Task<OpResult<T>>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk()
            : Task.FromResult(OpResults.Err<T>(result.Error!.Message));

    public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this OpResult<T> result,
        Func<T, Task<OpResult<TNext>>> onOk)
        where T : notnull
        where TNext : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : Task.FromResult(OpResults.Err<TNext>(result.Error!.Message));

    public static Task<OpResult> ThenAsync<T>(
        this OpResult<T> result,
        Func<T, Task<OpResult>> onOk)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : Task.FromResult(OpResults.Err(result.Error!.Message));

    public static async Task<OpResult> ThenAsync(
        this Task<OpResult> resultTask,
        Func<Task<OpResult>> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    public static async Task<OpResult<T>> ThenAsync<T>(
        this Task<OpResult> resultTask,
        Func<Task<OpResult<T>>> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    public static async Task<OpResult<TNext>> ThenAsync<T, TNext>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<OpResult<TNext>>> onOk)
        where T : notnull
        where TNext : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

    public static async Task<OpResult> ThenAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<OpResult>> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.ThenAsync(onOk).ConfigureAwait(false);
    }

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

    public static async Task<OpResult> OnOkAsync(
        this Task<OpResult> resultTask,
        Func<Task> onOk)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnOkAsync(onOk).ConfigureAwait(false);
    }

    public static async Task<OpResult<T>> OnOkAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task> onOk)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnOkAsync(onOk).ConfigureAwait(false);
    }

    public static async Task<OpResult> OnErrAsync(
        this Task<OpResult> resultTask,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnErrAsync(onErr).ConfigureAwait(false);
    }

    public static async Task<OpResult<T>> OnErrAsync<T>(
        this Task<OpResult<T>> resultTask,
        Func<OpError, Task> onErr)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.OnErrAsync(onErr).ConfigureAwait(false);
    }

    public static TResult Match<TResult>(
        this OpResult result,
        Func<TResult> onOk,
        Func<OpError, TResult> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

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

    public static TResult Match<T, TResult>(
        this OpResult<T> result,
        Func<T, TResult> onOk,
        Func<OpError, TResult> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

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

    public static Task<TResult> MatchAsync<TResult>(
        this OpResult result,
        Func<Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

    public static Task MatchAsync(
        this OpResult result,
        Func<Task> onOk,
        Func<OpError, Task> onErr) =>
        result.IsOk
            ? onOk()
            : onErr(result.Error!);

    public static Task<TResult> MatchAsync<T, TResult>(
        this OpResult<T> result,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

    public static Task MatchAsync<T>(
        this OpResult<T> result,
        Func<T, Task> onOk,
        Func<OpError, Task> onErr)
        where T : notnull =>
        result.IsOk
            ? onOk(result.Value)
            : onErr(result.Error!);

    public static async Task<TResult> MatchAsync<TResult>(
        this Task<OpResult> resultTask,
        Func<Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    public static async Task MatchAsync(
        this Task<OpResult> resultTask,
        Func<Task> onOk,
        Func<OpError, Task> onErr)
    {
        var result = await resultTask.ConfigureAwait(false);
        await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

    public static async Task<TResult> MatchAsync<T, TResult>(
        this Task<OpResult<T>> resultTask,
        Func<T, Task<TResult>> onOk,
        Func<OpError, Task<TResult>> onErr)
        where T : notnull
    {
        var result = await resultTask.ConfigureAwait(false);
        return await result.MatchAsync(onOk, onErr).ConfigureAwait(false);
    }

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
