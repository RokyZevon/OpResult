using System.Reflection;

namespace OpResult.Tests;

public sealed class TryInvokeTests
{
    [Fact]
    public void TryInvoke_SurfaceMatchesSpec()
    {
        var tryInvokeMethods = typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method =>
                method.Name is nameof(OpResults.TryInvoke) or nameof(OpResults.TryInvokeAsync))
            .ToArray();

        Assert.Equal(4, tryInvokeMethods.Length);

        var tryInvokeAction = FindMethod(
            nameof(OpResults.TryInvoke),
            typeof(OpResult),
            typeof(Action));

        var tryInvokeFunc = FindSyncValueMethod();

        var tryInvokeAsyncAction = FindMethod(
            nameof(OpResults.TryInvokeAsync),
            typeof(Task<OpResult>),
            typeof(Func<Task>));

        var tryInvokeAsyncFunc = FindAsyncValueMethod();

        Assert.Equal("action", tryInvokeAction.GetParameters().Single().Name);
        Assert.Equal("func", tryInvokeFunc.GetParameters().Single().Name);
        Assert.Equal("action", tryInvokeAsyncAction.GetParameters().Single().Name);
        Assert.Equal("func", tryInvokeAsyncFunc.GetParameters().Single().Name);
    }

    [Fact]
    public void TryInvoke_ActionReturnsOkWhenActionCompletes()
    {
        var called = false;

        var result = OpResults.TryInvoke(() => called = true);

        Assert.True(called);
        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
    }

    [Fact]
    public void TryInvoke_FuncReturnsOkValueWhenFuncReturnsNonNull()
    {
        var result = OpResults.TryInvoke(() => "loaded");

        Assert.True(result.IsOk);
        Assert.Equal("loaded", result.Value);
    }

    [Fact]
    public async Task TryInvokeAsync_ActionReturnsOkWhenTaskCompletes()
    {
        var called = false;

        var result = await OpResults.TryInvokeAsync(() =>
        {
            called = true;
            return Task.CompletedTask;
        });

        Assert.True(called);
        Assert.True(result.IsOk);
        Assert.False(result.IsErr);
    }

    [Fact]
    public async Task TryInvokeAsync_FuncReturnsOkValueWhenTaskReturnsNonNull()
    {
        var result = await OpResults.TryInvokeAsync(() => Task.FromResult("loaded"));

        Assert.True(result.IsOk);
        Assert.Equal("loaded", result.Value);
    }

    [Fact]
    public void TryInvoke_NullDelegatesThrowArgumentNullException()
    {
        var actionException = Assert.Throws<ArgumentNullException>(() =>
            OpResults.TryInvoke((Action)null!));
        var funcException = Assert.Throws<ArgumentNullException>(() =>
            OpResults.TryInvoke((Func<string>)null!));

        Assert.Equal("action", actionException.ParamName);
        Assert.Equal("func", funcException.ParamName);
    }

    [Fact]
    public async Task TryInvokeAsync_NullDelegatesThrowArgumentNullException()
    {
        var actionException = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            OpResults.TryInvokeAsync((Func<Task>)null!));
        var funcException = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            OpResults.TryInvokeAsync((Func<Task<string>>)null!));

        Assert.Equal("action", actionException.ParamName);
        Assert.Equal("func", funcException.ParamName);
    }

    [Fact]
    public void TryInvoke_SyncExceptionsReturnErrWithOriginalMessage()
    {
        var actionResult = OpResults.TryInvoke(() => throw new InvalidOperationException("action failed"));
        var funcResult = OpResults.TryInvoke<string>(() => throw new InvalidOperationException("func failed"));

        Assert.True(actionResult.IsErr);
        Assert.Equal("action failed", actionResult.Error!.Message);
        Assert.True(funcResult.IsErr);
        Assert.Equal("func failed", funcResult.Error!.Message);
    }

    [Fact]
    public async Task TryInvokeAsync_ExceptionsReturnErrWithOriginalMessage()
    {
        var actionResult = await OpResults.TryInvokeAsync(() => throw new InvalidOperationException("async action failed"));
        var funcResult = await OpResults.TryInvokeAsync<string>(() => throw new InvalidOperationException("async func failed"));
        var actionTaskResult = await OpResults.TryInvokeAsync(() =>
            Task.FromException(new InvalidOperationException("async action task failed")));
        var funcTaskResult = await OpResults.TryInvokeAsync<string>(() =>
            Task.FromException<string>(new InvalidOperationException("async func task failed")));

        Assert.True(actionResult.IsErr);
        Assert.Equal("async action failed", actionResult.Error!.Message);
        Assert.True(funcResult.IsErr);
        Assert.Equal("async func failed", funcResult.Error!.Message);
        Assert.True(actionTaskResult.IsErr);
        Assert.Equal("async action task failed", actionTaskResult.Error!.Message);
        Assert.True(funcTaskResult.IsErr);
        Assert.Equal("async func task failed", funcTaskResult.Error!.Message);
    }

    [Fact]
    public void TryInvoke_NullPayloadReturnsErrWithFixedMessage()
    {
        var result = OpResults.TryInvoke<string>(() => null!);

        Assert.True(result.IsErr);
        Assert.Equal("Operation returned null.", result.Error!.Message);
    }

    [Fact]
    public async Task TryInvokeAsync_NullTaskReturnsErrWithFixedMessage()
    {
        var actionResult = await OpResults.TryInvokeAsync(() => null!);
        var funcResult = await OpResults.TryInvokeAsync<string>(() => null!);

        Assert.True(actionResult.IsErr);
        Assert.Equal("Operation returned null.", actionResult.Error!.Message);
        Assert.True(funcResult.IsErr);
        Assert.Equal("Operation returned null.", funcResult.Error!.Message);
    }

    [Fact]
    public async Task TryInvokeAsync_NullPayloadReturnsErrWithFixedMessage()
    {
        var result = await OpResults.TryInvokeAsync(() => Task.FromResult<string>(null!));

        Assert.True(result.IsErr);
        Assert.Equal("Operation returned null.", result.Error!.Message);
    }

    [Fact]
    public void TryInvoke_CancellationExceptionsPropagate()
    {
        Assert.Throws<OperationCanceledException>(() =>
            OpResults.TryInvoke(() => throw new OperationCanceledException()));
        Assert.Throws<TaskCanceledException>(() =>
            OpResults.TryInvoke<string>(() => throw new TaskCanceledException()));
    }

    [Fact]
    public async Task TryInvokeAsync_CancellationExceptionsPropagate()
    {
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            OpResults.TryInvokeAsync(() => throw new OperationCanceledException()));
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            OpResults.TryInvokeAsync<string>(() => throw new TaskCanceledException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OpResults.TryInvokeAsync(() => Task.FromCanceled(new CancellationToken(true))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            OpResults.TryInvokeAsync<string>(() => Task.FromCanceled<string>(new CancellationToken(true))));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            OpResults.TryInvokeAsync(() => Task.FromException(new OperationCanceledException())));
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            OpResults.TryInvokeAsync<string>(() => Task.FromException<string>(new TaskCanceledException())));
    }

    private static MethodInfo FindMethod(string name, Type returnType, Type parameterType) =>
        typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                var parameters = method.GetParameters();
                return method.Name == name
                    && !method.IsGenericMethod
                    && method.ReturnType == returnType
                    && parameters.Length == 1
                    && parameters[0].ParameterType == parameterType;
            });

    private static MethodInfo FindSyncValueMethod() =>
        typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (method.Name != nameof(OpResults.TryInvoke) || !method.IsGenericMethodDefinition)
                {
                    return false;
                }

                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length != 1)
                {
                    return false;
                }

                var genericValueType = genericArguments[0];
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                {
                    return false;
                }

                var parameterType = parameters[0].ParameterType;
                if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(Func<>))
                {
                    return false;
                }

                var parameterGenericArgument = parameterType.GetGenericArguments()[0];
                if (parameterGenericArgument != genericValueType)
                {
                    return false;
                }

                var returnType = method.ReturnType;
                return returnType.IsGenericType
                    && returnType.GetGenericTypeDefinition() == typeof(OpResult<>)
                    && returnType.GetGenericArguments()[0] == genericValueType;
            });

    private static MethodInfo FindAsyncValueMethod() =>
        typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (method.Name != nameof(OpResults.TryInvokeAsync) || !method.IsGenericMethodDefinition)
                {
                    return false;
                }

                var genericArguments = method.GetGenericArguments();
                if (genericArguments.Length != 1)
                {
                    return false;
                }

                var genericValueType = genericArguments[0];
                var parameters = method.GetParameters();
                if (parameters.Length != 1)
                {
                    return false;
                }

                var parameterType = parameters[0].ParameterType;
                if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(Func<>))
                {
                    return false;
                }

                var funcReturnType = parameterType.GetGenericArguments()[0];
                if (!funcReturnType.IsGenericType || funcReturnType.GetGenericTypeDefinition() != typeof(Task<>))
                {
                    return false;
                }

                if (funcReturnType.GetGenericArguments()[0] != genericValueType)
                {
                    return false;
                }

                var returnType = method.ReturnType;
                if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
                {
                    return false;
                }

                var taskInnerType = returnType.GetGenericArguments()[0];
                return taskInnerType.IsGenericType
                    && taskInnerType.GetGenericTypeDefinition() == typeof(OpResult<>)
                    && taskInnerType.GetGenericArguments()[0] == genericValueType;
            });
}
