namespace OpResult.Tests;

using System.Reflection;

public class WorkflowTests
{
    [Fact]
    public void Then_MatrixSurfaceMatchesSpec()
    {
        var thenVoidToVoid = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<OpResult>);
            });

        var thenVoidToValue = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments.Single();
                var parameters = method.GetParameters();
                var expectedResultType = OpResultOf(t);

                return method.ReturnType == expectedResultType &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, expectedResultType);
            });

        var thenValueToValue = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tNext = typeArguments[1];
                var parameters = method.GetParameters();
                var expectedParameterType = OpResultOf(t);
                var expectedReturnType = OpResultOf(tNext);

                return method.ReturnType == expectedReturnType &&
                    parameters[0].ParameterType == expectedParameterType &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, expectedReturnType);
            });

        var thenValueToVoid = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments.Single();
                var parameters = method.GetParameters();
                var expectedReceiverType = OpResultOf(t);

                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == expectedReceiverType &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(OpResult));
            });

        Assert.NotNull(thenVoidToVoid);
        Assert.NotNull(thenVoidToValue);
        Assert.NotNull(thenValueToValue);
        Assert.NotNull(thenValueToVoid);
    }

    [Fact]
    public void Then_ErrPathShortCircuits_OkPathRunsContinuation()
    {
        var thenVoidToVoid = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<OpResult>);
            });

        var thenVoidToValueDefinition = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                var expectedResultType = OpResultOf(t);
                return method.ReturnType == expectedResultType &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, expectedResultType);
            });

        var thenValueToValueDefinition = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tNext = typeArguments[1];
                var parameters = method.GetParameters();
                var expectedReturnType = OpResultOf(tNext);
                return method.ReturnType == expectedReturnType &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, expectedReturnType);
            });

        var thenValueToVoidDefinition = FindWorkflowMethod(
            name: "Then",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(OpResult));
            });

        OpResult errForVoidToVoid = OpResults.Err("void-to-void short-circuit");
        var voidToVoidCalled = false;
        Func<OpResult> voidToVoidContinuation = () =>
        {
            voidToVoidCalled = true;
            return OpResults.Ok();
        };

        var voidToVoidErrResult = InvokeWorkflow<OpResult>(thenVoidToVoid, errForVoidToVoid, voidToVoidContinuation);
        Assert.False(voidToVoidCalled);
        Assert.True(voidToVoidErrResult.IsErr);
        Assert.Equal(errForVoidToVoid.Error!.Message, voidToVoidErrResult.Error!.Message);

        var voidToVoidOkCalled = false;
        Func<OpResult> voidToVoidOkContinuation = () =>
        {
            voidToVoidOkCalled = true;
            return OpResults.Ok();
        };

        var voidToVoidOkResult = InvokeWorkflow<OpResult>(thenVoidToVoid, OpResults.Ok(), voidToVoidOkContinuation);
        Assert.True(voidToVoidOkCalled);
        Assert.True(voidToVoidOkResult.IsOk);

        var thenVoidToValue = thenVoidToValueDefinition.MakeGenericMethod(typeof(int));
        OpResult errForVoidToValue = OpResults.Err("void-to-value short-circuit");
        var voidToValueCalled = false;
        Func<OpResult<int>> voidToValueContinuation = () =>
        {
            voidToValueCalled = true;
            return OpResults.Ok(7);
        };

        var voidToValueErrResult = InvokeWorkflow<OpResult<int>>(thenVoidToValue, errForVoidToValue, voidToValueContinuation);
        Assert.False(voidToValueCalled);
        Assert.True(voidToValueErrResult.IsErr);
        Assert.Equal(errForVoidToValue.Error!.Message, voidToValueErrResult.Error!.Message);

        var voidToValueOkCalled = false;
        Func<OpResult<int>> voidToValueOkContinuation = () =>
        {
            voidToValueOkCalled = true;
            return OpResults.Ok(11);
        };

        var voidToValueOkResult = InvokeWorkflow<OpResult<int>>(thenVoidToValue, OpResults.Ok(), voidToValueOkContinuation);
        Assert.True(voidToValueOkCalled);
        Assert.True(voidToValueOkResult.IsOk);
        Assert.Equal(11, voidToValueOkResult.Value);

        var thenValueToValue = thenValueToValueDefinition.MakeGenericMethod(typeof(int), typeof(string));
        var errForValueToValue = OpResults.Err<int>("value-to-value short-circuit");
        var valueToValueCalled = false;
        Func<int, OpResult<string>> valueToValueContinuation = value =>
        {
            valueToValueCalled = true;
            return OpResults.Ok($"v-{value}");
        };

        var valueToValueErrResult = InvokeWorkflow<OpResult<string>>(thenValueToValue, errForValueToValue, valueToValueContinuation);
        Assert.False(valueToValueCalled);
        Assert.True(valueToValueErrResult.IsErr);
        Assert.Equal(errForValueToValue.Error!.Message, valueToValueErrResult.Error!.Message);

        var valueToValueOkCalled = false;
        Func<int, OpResult<string>> valueToValueOkContinuation = value =>
        {
            valueToValueOkCalled = true;
            return OpResults.Ok($"ok-{value + 1}");
        };

        var valueToValueOkResult = InvokeWorkflow<OpResult<string>>(thenValueToValue, OpResults.Ok(3), valueToValueOkContinuation);
        Assert.True(valueToValueOkCalled);
        Assert.True(valueToValueOkResult.IsOk);
        Assert.Equal("ok-4", valueToValueOkResult.Value);

        var thenValueToVoid = thenValueToVoidDefinition.MakeGenericMethod(typeof(int));
        var errForValueToVoid = OpResults.Err<int>("value-to-void short-circuit");
        var valueToVoidCalled = false;
        Func<int, OpResult> valueToVoidContinuation = value =>
        {
            valueToVoidCalled = true;
            return OpResults.Ok();
        };

        var valueToVoidErrResult = InvokeWorkflow<OpResult>(thenValueToVoid, errForValueToVoid, valueToVoidContinuation);
        Assert.False(valueToVoidCalled);
        Assert.True(valueToVoidErrResult.IsErr);
        Assert.Equal(errForValueToVoid.Error!.Message, valueToVoidErrResult.Error!.Message);

        var valueToVoidOkCalled = false;
        Func<int, OpResult> valueToVoidOkContinuation = value =>
        {
            valueToVoidOkCalled = true;
            return OpResults.Ok();
        };

        var valueToVoidOkResult = InvokeWorkflow<OpResult>(thenValueToVoid, OpResults.Ok(9), valueToVoidOkContinuation);
        Assert.True(valueToVoidOkCalled);
        Assert.True(valueToVoidOkResult.IsOk);
    }

    [Fact]
    public void Then_WhenSourceIsErrPreservesOriginalErrorReference()
    {
        var originalError = OpResults.Err("database failed").ToErr("get user failed");

        OpResult voidSource = originalError;
        var voidToVoid = voidSource.Then(() => throw new InvalidOperationException("should not run"));
        OpResult<int> voidToValue = voidSource.Then<int>(() => throw new InvalidOperationException("should not run"));

        OpResult<int> valueSource = originalError;
        OpResult<string> valueToValue = valueSource.Then<int, string>(_ => throw new InvalidOperationException("should not run"));
        OpResult valueToVoid = valueSource.Then<int>(_ => throw new InvalidOperationException("should not run"));

        Assert.True(voidToVoid.IsErr);
        Assert.Same(originalError, voidToVoid.Error);
        Assert.True(voidToValue.IsErr);
        Assert.Same(originalError, voidToValue.Error);
        Assert.True(valueToValue.IsErr);
        Assert.Same(originalError, valueToValue.Error);
        Assert.True(valueToVoid.IsErr);
        Assert.Same(originalError, valueToVoid.Error);
    }

    [Fact]
    public void ThenAsync_MatrixSurfaceMatchesSpec()
    {
        var thenAsyncVoidToVoid = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<Task<OpResult>>);
            });

        var thenAsyncVoidToValue = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                var expectedResultType = OpResultOf(t);
                var expectedTaskResultType = TaskOf(expectedResultType);
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, expectedTaskResultType);
            });

        var thenAsyncValueToValue = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tNext = typeArguments[1];
                var parameters = method.GetParameters();
                var expectedReceiverType = OpResultOf(t);
                var expectedResultType = OpResultOf(tNext);
                var expectedTaskResultType = TaskOf(expectedResultType);
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == expectedReceiverType &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, expectedTaskResultType);
            });

        var thenAsyncValueToVoid = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(typeof(OpResult)));
            });

        var thenAsyncTaskVoidToVoid = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task<OpResult>>);
            });

        var thenAsyncTaskVoidToValue = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                var expectedResultType = OpResultOf(t);
                var expectedTaskResultType = TaskOf(expectedResultType);
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    IsFuncOfReturn(parameters[1].ParameterType, expectedTaskResultType);
            });

        var thenAsyncTaskValueToValue = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tNext = typeArguments[1];
                var parameters = method.GetParameters();
                var expectedReceiverType = TaskOf(OpResultOf(t));
                var expectedResultType = OpResultOf(tNext);
                var expectedTaskResultType = TaskOf(expectedResultType);
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == expectedReceiverType &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, expectedTaskResultType);
            });

        var thenAsyncTaskValueToVoid = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(typeof(OpResult)));
            });

        Assert.NotNull(thenAsyncVoidToVoid);
        Assert.NotNull(thenAsyncVoidToValue);
        Assert.NotNull(thenAsyncValueToValue);
        Assert.NotNull(thenAsyncValueToVoid);
        Assert.NotNull(thenAsyncTaskVoidToVoid);
        Assert.NotNull(thenAsyncTaskVoidToValue);
        Assert.NotNull(thenAsyncTaskValueToValue);
        Assert.NotNull(thenAsyncTaskValueToVoid);
    }

    [Fact]
    public async Task ThenAsync_WhenSourceIsErrPreservesOriginalErrorReference()
    {
        var originalError = OpResults.Err("database failed").ToErr("get user failed");

        OpResult voidSource = originalError;
        var voidToVoid = await voidSource.ThenAsync(() => throw new InvalidOperationException("should not run"));
        var voidToValue = await voidSource.ThenAsync<int>(() => throw new InvalidOperationException("should not run"));

        OpResult<int> valueSource = originalError;
        var valueToValue = await valueSource.ThenAsync<int, string>(_ => throw new InvalidOperationException("should not run"));
        var valueToVoid = await valueSource.ThenAsync<int>(_ => throw new InvalidOperationException("should not run"));

        Assert.True(voidToVoid.IsErr);
        Assert.Same(originalError, voidToVoid.Error);
        Assert.True(voidToValue.IsErr);
        Assert.Same(originalError, voidToValue.Error);
        Assert.True(valueToValue.IsErr);
        Assert.Same(originalError, valueToValue.Error);
        Assert.True(valueToVoid.IsErr);
        Assert.Same(originalError, valueToVoid.Error);
    }

    [Fact]
    public async Task ThenAsync_CommonChains_RunAndShortCircuit()
    {
        var thenAsyncTaskVoidToVoid = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task<OpResult>>);
            });

        var thenAsyncTaskVoidToValueDefinition = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                var expectedTaskResultType = TaskOf(OpResultOf(t));
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    IsFuncOfReturn(parameters[1].ParameterType, expectedTaskResultType);
            });

        var thenAsyncTaskValueToValueDefinition = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tNext = typeArguments[1];
                var parameters = method.GetParameters();
                var expectedTaskResultType = TaskOf(OpResultOf(tNext));
                return method.ReturnType == expectedTaskResultType &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, expectedTaskResultType);
            });

        var thenAsyncTaskValueToVoidDefinition = FindWorkflowMethod(
            name: "ThenAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(typeof(OpResult)));
            });

        var voidToVoidErrCalled = false;
        Func<Task<OpResult>> voidToVoidContinuation = () =>
        {
            voidToVoidErrCalled = true;
            return Task.FromResult(OpResults.Ok());
        };

        var voidToVoidErrResult = await InvokeWorkflowAsync<OpResult>(
            thenAsyncTaskVoidToVoid,
            Task.FromResult<OpResult>(OpResults.Err("task-void-to-void short-circuit")),
            voidToVoidContinuation);

        Assert.False(voidToVoidErrCalled);
        Assert.True(voidToVoidErrResult.IsErr);
        Assert.Equal("task-void-to-void short-circuit", voidToVoidErrResult.Error!.Message);

        var voidToVoidOkCalled = false;
        Func<Task<OpResult>> voidToVoidOkContinuation = () =>
        {
            voidToVoidOkCalled = true;
            return Task.FromResult(OpResults.Ok());
        };

        var voidToVoidOkResult = await InvokeWorkflowAsync<OpResult>(
            thenAsyncTaskVoidToVoid,
            Task.FromResult(OpResults.Ok()),
            voidToVoidOkContinuation);

        Assert.True(voidToVoidOkCalled);
        Assert.True(voidToVoidOkResult.IsOk);

        var thenAsyncTaskVoidToValue = thenAsyncTaskVoidToValueDefinition.MakeGenericMethod(typeof(int));

        var voidToValueErrCalled = false;
        Func<Task<OpResult<int>>> voidToValueContinuation = () =>
        {
            voidToValueErrCalled = true;
            return Task.FromResult(OpResults.Ok(5));
        };

        var voidToValueErrResult = await InvokeWorkflowAsync<OpResult<int>>(
            thenAsyncTaskVoidToValue,
            Task.FromResult<OpResult>(OpResults.Err("task-void-to-value short-circuit")),
            voidToValueContinuation);

        Assert.False(voidToValueErrCalled);
        Assert.True(voidToValueErrResult.IsErr);
        Assert.Equal("task-void-to-value short-circuit", voidToValueErrResult.Error!.Message);

        var voidToValueOkCalled = false;
        Func<Task<OpResult<int>>> voidToValueOkContinuation = () =>
        {
            voidToValueOkCalled = true;
            return Task.FromResult(OpResults.Ok(6));
        };

        var voidToValueOkResult = await InvokeWorkflowAsync<OpResult<int>>(
            thenAsyncTaskVoidToValue,
            Task.FromResult(OpResults.Ok()),
            voidToValueOkContinuation);

        Assert.True(voidToValueOkCalled);
        Assert.True(voidToValueOkResult.IsOk);
        Assert.Equal(6, voidToValueOkResult.Value);

        var thenAsyncTaskValueToValue = thenAsyncTaskValueToValueDefinition.MakeGenericMethod(typeof(int), typeof(string));

        var valueToValueErrCalled = false;
        Func<int, Task<OpResult<string>>> valueToValueContinuation = value =>
        {
            valueToValueErrCalled = true;
            return Task.FromResult(OpResults.Ok($"n-{value}"));
        };

        var valueToValueErrResult = await InvokeWorkflowAsync<OpResult<string>>(
            thenAsyncTaskValueToValue,
            Task.FromResult(OpResults.Err<int>("task-value-to-value short-circuit")),
            valueToValueContinuation);

        Assert.False(valueToValueErrCalled);
        Assert.True(valueToValueErrResult.IsErr);
        Assert.Equal("task-value-to-value short-circuit", valueToValueErrResult.Error!.Message);

        var valueToValueOkCalled = false;
        Func<int, Task<OpResult<string>>> valueToValueOkContinuation = value =>
        {
            valueToValueOkCalled = true;
            return Task.FromResult(OpResults.Ok($"ok-{value + 2}"));
        };

        var valueToValueOkResult = await InvokeWorkflowAsync<OpResult<string>>(
            thenAsyncTaskValueToValue,
            Task.FromResult(OpResults.Ok(8)),
            valueToValueOkContinuation);

        Assert.True(valueToValueOkCalled);
        Assert.True(valueToValueOkResult.IsOk);
        Assert.Equal("ok-10", valueToValueOkResult.Value);

        var thenAsyncTaskValueToVoid = thenAsyncTaskValueToVoidDefinition.MakeGenericMethod(typeof(int));

        var valueToVoidErrCalled = false;
        Func<int, Task<OpResult>> valueToVoidContinuation = value =>
        {
            valueToVoidErrCalled = true;
            return Task.FromResult(OpResults.Ok());
        };

        var valueToVoidErrResult = await InvokeWorkflowAsync<OpResult>(
            thenAsyncTaskValueToVoid,
            Task.FromResult(OpResults.Err<int>("task-value-to-void short-circuit")),
            valueToVoidContinuation);

        Assert.False(valueToVoidErrCalled);
        Assert.True(valueToVoidErrResult.IsErr);
        Assert.Equal("task-value-to-void short-circuit", valueToVoidErrResult.Error!.Message);

        var valueToVoidOkCalled = false;
        Func<int, Task<OpResult>> valueToVoidOkContinuation = value =>
        {
            valueToVoidOkCalled = true;
            return Task.FromResult(OpResults.Ok());
        };

        var valueToVoidOkResult = await InvokeWorkflowAsync<OpResult>(
            thenAsyncTaskValueToVoid,
            Task.FromResult(OpResults.Ok(12)),
            valueToVoidOkContinuation);

        Assert.True(valueToVoidOkCalled);
        Assert.True(valueToVoidOkResult.IsOk);
    }

    [Fact]
    public void OnOkAndOnErr_MatrixSurfaceMatchesSpec()
    {
        var onOkVoid = FindWorkflowMethod(
            name: "OnOk",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action);
            });

        var onOkValue = FindWorkflowMethod(
            name: "OnOk",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == OpResultOf(t) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsActionOf(parameters[1].ParameterType, t);
            });

        var onErrVoid = FindWorkflowMethod(
            name: "OnErr",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action<OpError>);
            });

        var onErrValue = FindWorkflowMethod(
            name: "OnErr",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == OpResultOf(t) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    parameters[1].ParameterType == typeof(Action<OpError>);
            });

        var onOkAsyncVoid = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<Task>);
            });

        var onOkAsyncValue = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(Task));
            });

        var onErrAsyncVoid = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        var onErrAsyncValue = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        var onOkAsyncTaskVoid = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task>);
            });

        var onOkAsyncTaskValue = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(Task));
            });

        var onErrAsyncTaskVoid = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        var onErrAsyncTaskValue = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        Assert.NotNull(onOkVoid);
        Assert.NotNull(onOkValue);
        Assert.NotNull(onErrVoid);
        Assert.NotNull(onErrValue);
        Assert.NotNull(onOkAsyncVoid);
        Assert.NotNull(onOkAsyncValue);
        Assert.NotNull(onErrAsyncVoid);
        Assert.NotNull(onErrAsyncValue);
        Assert.NotNull(onOkAsyncTaskVoid);
        Assert.NotNull(onOkAsyncTaskValue);
        Assert.NotNull(onErrAsyncTaskVoid);
        Assert.NotNull(onErrAsyncTaskValue);
    }

    [Fact]
    public async Task OnObservers_AreBranchSelective_AndReturnOriginalResult()
    {
        var onOkVoid = FindWorkflowMethod(
            name: "OnOk",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action);
            });

        var onOkValueDefinition = FindWorkflowMethod(
            name: "OnOk",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == OpResultOf(t) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsActionOf(parameters[1].ParameterType, t);
            });

        var onErrVoid = FindWorkflowMethod(
            name: "OnErr",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(OpResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action<OpError>);
            });

        var onErrValueDefinition = FindWorkflowMethod(
            name: "OnErr",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == OpResultOf(t) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    parameters[1].ParameterType == typeof(Action<OpError>);
            });

        var onOkAsyncTaskVoid = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task>);
            });

        var onErrAsyncTaskVoid = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(typeof(OpResult)) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        var onOkAsyncTaskValueDefinition = FindWorkflowMethod(
            name: "OnOkAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(Task));
            });

        var onErrAsyncTaskValueDefinition = FindWorkflowMethod(
            name: "OnErrAsync",
            parameterCount: 2,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(OpResultOf(t)) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    parameters[1].ParameterType == typeof(Func<OpError, Task>);
            });

        var onOkVoidCalled = 0;
        Action onOkVoidAction = () => onOkVoidCalled++;
        var okVoidInput = OpResults.Ok();
        var okVoidReturned = InvokeWorkflow<OpResult>(onOkVoid, okVoidInput, onOkVoidAction);
        Assert.Equal(1, onOkVoidCalled);
        Assert.Equal(okVoidInput, okVoidReturned);

        var onOkVoidErrCalled = 0;
        Action onOkVoidErrAction = () => onOkVoidErrCalled++;
        OpResult errVoidInput = OpResults.Err("onok-void");
        var errVoidReturned = InvokeWorkflow<OpResult>(onOkVoid, errVoidInput, onOkVoidErrAction);
        Assert.Equal(0, onOkVoidErrCalled);
        Assert.Equal(errVoidInput, errVoidReturned);

        var onOkValue = onOkValueDefinition.MakeGenericMethod(typeof(int));
        var onOkValueCalled = 0;
        var okValueInput = OpResults.Ok(20);
        Action<int> onOkValueAction = value =>
        {
            Assert.Equal(20, value);
            onOkValueCalled++;
        };
        var okValueReturned = InvokeWorkflow<OpResult<int>>(onOkValue, okValueInput, onOkValueAction);
        Assert.Equal(1, onOkValueCalled);
        Assert.Equal(okValueInput, okValueReturned);

        var onOkValueErrCalled = 0;
        var errValueInput = OpResults.Err<int>("onok-value");
        Action<int> onOkValueErrAction = value => onOkValueErrCalled++;
        var errValueReturned = InvokeWorkflow<OpResult<int>>(onOkValue, errValueInput, onOkValueErrAction);
        Assert.Equal(0, onOkValueErrCalled);
        Assert.Equal(errValueInput, errValueReturned);

        var onErrVoidCalled = 0;
        Action<OpError> onErrVoidAction = _ => onErrVoidCalled++;
        var onErrVoidOkReturned = InvokeWorkflow<OpResult>(onErrVoid, OpResults.Ok(), onErrVoidAction);
        Assert.Equal(0, onErrVoidCalled);
        Assert.True(onErrVoidOkReturned.IsOk);

        var onErrVoidErrCalled = 0;
        Action<OpError> onErrVoidErrAction = error =>
        {
            Assert.Equal("onerr-void", error.Message);
            onErrVoidErrCalled++;
        };
        OpResult onErrVoidErrInput = OpResults.Err("onerr-void");
        var onErrVoidErrReturned = InvokeWorkflow<OpResult>(onErrVoid, onErrVoidErrInput, onErrVoidErrAction);
        Assert.Equal(1, onErrVoidErrCalled);
        Assert.Equal(onErrVoidErrInput, onErrVoidErrReturned);

        var onErrValue = onErrValueDefinition.MakeGenericMethod(typeof(int));
        var onErrValueOkCalled = 0;
        Action<OpError> onErrValueOkAction = _ => onErrValueOkCalled++;
        var onErrValueOkInput = OpResults.Ok(99);
        var onErrValueOkReturned = InvokeWorkflow<OpResult<int>>(onErrValue, onErrValueOkInput, onErrValueOkAction);
        Assert.Equal(0, onErrValueOkCalled);
        Assert.Equal(onErrValueOkInput, onErrValueOkReturned);

        var onErrValueErrCalled = 0;
        Action<OpError> onErrValueErrAction = error =>
        {
            Assert.Equal("onerr-value", error.Message);
            onErrValueErrCalled++;
        };
        var onErrValueErrInput = OpResults.Err<int>("onerr-value");
        var onErrValueErrReturned = InvokeWorkflow<OpResult<int>>(onErrValue, onErrValueErrInput, onErrValueErrAction);
        Assert.Equal(1, onErrValueErrCalled);
        Assert.Equal(onErrValueErrInput, onErrValueErrReturned);

        var onOkAsyncTaskVoidCalled = 0;
        Func<Task> onOkAsyncTaskVoidAction = () =>
        {
            onOkAsyncTaskVoidCalled++;
            return Task.CompletedTask;
        };
        var onOkAsyncTaskVoidErrReturned = await InvokeWorkflowAsync<OpResult>(
            onOkAsyncTaskVoid,
            Task.FromResult<OpResult>(OpResults.Err("onokasync-task-void")),
            onOkAsyncTaskVoidAction);
        Assert.Equal(0, onOkAsyncTaskVoidCalled);
        Assert.True(onOkAsyncTaskVoidErrReturned.IsErr);

        var onOkAsyncTaskVoidOkCalled = 0;
        Func<Task> onOkAsyncTaskVoidOkAction = () =>
        {
            onOkAsyncTaskVoidOkCalled++;
            return Task.CompletedTask;
        };
        var onOkAsyncTaskVoidOkReturned = await InvokeWorkflowAsync<OpResult>(
            onOkAsyncTaskVoid,
            Task.FromResult(OpResults.Ok()),
            onOkAsyncTaskVoidOkAction);
        Assert.Equal(1, onOkAsyncTaskVoidOkCalled);
        Assert.True(onOkAsyncTaskVoidOkReturned.IsOk);

        var onErrAsyncTaskVoidCalled = 0;
        Func<OpError, Task> onErrAsyncTaskVoidAction = _ =>
        {
            onErrAsyncTaskVoidCalled++;
            return Task.CompletedTask;
        };
        var onErrAsyncTaskVoidOkReturned = await InvokeWorkflowAsync<OpResult>(
            onErrAsyncTaskVoid,
            Task.FromResult(OpResults.Ok()),
            onErrAsyncTaskVoidAction);
        Assert.Equal(0, onErrAsyncTaskVoidCalled);
        Assert.True(onErrAsyncTaskVoidOkReturned.IsOk);

        var onErrAsyncTaskVoidErrCalled = 0;
        Func<OpError, Task> onErrAsyncTaskVoidErrAction = error =>
        {
            Assert.Equal("onerrasync-task-void", error.Message);
            onErrAsyncTaskVoidErrCalled++;
            return Task.CompletedTask;
        };
        OpResult onErrAsyncTaskVoidErrInput = OpResults.Err("onerrasync-task-void");
        var onErrAsyncTaskVoidErrReturned = await InvokeWorkflowAsync<OpResult>(
            onErrAsyncTaskVoid,
            Task.FromResult<OpResult>(onErrAsyncTaskVoidErrInput),
            onErrAsyncTaskVoidErrAction);
        Assert.Equal(1, onErrAsyncTaskVoidErrCalled);
        Assert.Equal(onErrAsyncTaskVoidErrInput, onErrAsyncTaskVoidErrReturned);

        var onOkAsyncTaskValue = onOkAsyncTaskValueDefinition.MakeGenericMethod(typeof(int));
        var onOkAsyncTaskValueCalled = 0;
        Func<int, Task> onOkAsyncTaskValueAction = value =>
        {
            Assert.Equal(13, value);
            onOkAsyncTaskValueCalled++;
            return Task.CompletedTask;
        };
        var onOkAsyncTaskValueOkInput = OpResults.Ok(13);
        var onOkAsyncTaskValueOkReturned = await InvokeWorkflowAsync<OpResult<int>>(
            onOkAsyncTaskValue,
            Task.FromResult(onOkAsyncTaskValueOkInput),
            onOkAsyncTaskValueAction);
        Assert.Equal(1, onOkAsyncTaskValueCalled);
        Assert.Equal(onOkAsyncTaskValueOkInput, onOkAsyncTaskValueOkReturned);

        var onOkAsyncTaskValueErrCalled = 0;
        Func<int, Task> onOkAsyncTaskValueErrAction = _ =>
        {
            onOkAsyncTaskValueErrCalled++;
            return Task.CompletedTask;
        };
        var onOkAsyncTaskValueErrInput = OpResults.Err<int>("onokasync-task-value");
        var onOkAsyncTaskValueErrReturned = await InvokeWorkflowAsync<OpResult<int>>(
            onOkAsyncTaskValue,
            Task.FromResult(onOkAsyncTaskValueErrInput),
            onOkAsyncTaskValueErrAction);
        Assert.Equal(0, onOkAsyncTaskValueErrCalled);
        Assert.Equal(onOkAsyncTaskValueErrInput, onOkAsyncTaskValueErrReturned);

        var onErrAsyncTaskValue = onErrAsyncTaskValueDefinition.MakeGenericMethod(typeof(int));
        var onErrAsyncTaskValueOkCalled = 0;
        Func<OpError, Task> onErrAsyncTaskValueOkAction = _ =>
        {
            onErrAsyncTaskValueOkCalled++;
            return Task.CompletedTask;
        };
        var onErrAsyncTaskValueOkInput = OpResults.Ok(15);
        var onErrAsyncTaskValueOkReturned = await InvokeWorkflowAsync<OpResult<int>>(
            onErrAsyncTaskValue,
            Task.FromResult(onErrAsyncTaskValueOkInput),
            onErrAsyncTaskValueOkAction);
        Assert.Equal(0, onErrAsyncTaskValueOkCalled);
        Assert.Equal(onErrAsyncTaskValueOkInput, onErrAsyncTaskValueOkReturned);

        var onErrAsyncTaskValueErrCalled = 0;
        Func<OpError, Task> onErrAsyncTaskValueErrAction = error =>
        {
            Assert.Equal("onerrasync-task-value", error.Message);
            onErrAsyncTaskValueErrCalled++;
            return Task.CompletedTask;
        };
        var onErrAsyncTaskValueErrInput = OpResults.Err<int>("onerrasync-task-value");
        var onErrAsyncTaskValueErrReturned = await InvokeWorkflowAsync<OpResult<int>>(
            onErrAsyncTaskValue,
            Task.FromResult(onErrAsyncTaskValueErrInput),
            onErrAsyncTaskValueErrAction);
        Assert.Equal(1, onErrAsyncTaskValueErrCalled);
        Assert.Equal(onErrAsyncTaskValueErrInput, onErrAsyncTaskValueErrReturned);
    }

    [Fact]
    public void MatchAndMatchAsync_MatrixSurfaceMatchesSpec()
    {
        var matchVoidFold = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var tResult = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == tResult &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, tResult) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), tResult);
            });

        var matchVoidAction = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action) &&
                    parameters[2].ParameterType == typeof(Action<OpError>);
            });

        var matchValueFold = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tResult = typeArguments[1];
                var parameters = method.GetParameters();
                return method.ReturnType == tResult &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, tResult) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), tResult);
            });

        var matchValueAction = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsActionOf(parameters[1].ParameterType, t) &&
                    parameters[2].ParameterType == typeof(Action<OpError>);
            });

        var matchAsyncVoidFold = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var tResult = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(tResult) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, TaskOf(tResult)) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), TaskOf(tResult));
            });

        var matchAsyncVoidAction = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(Task) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Func<Task>) &&
                    parameters[2].ParameterType == typeof(Func<OpError, Task>);
            });

        var matchAsyncValueFold = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tResult = typeArguments[1];
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(tResult) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(tResult)) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), TaskOf(tResult));
            });

        var matchAsyncValueAction = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(Task) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(Task)) &&
                    parameters[2].ParameterType == typeof(Func<OpError, Task>);
            });

        var matchAsyncTaskVoidFold = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var tResult = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(tResult) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    IsFuncOfReturn(parameters[1].ParameterType, TaskOf(tResult)) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), TaskOf(tResult));
            });

        var matchAsyncTaskVoidAction = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(Task) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task>) &&
                    parameters[2].ParameterType == typeof(Func<OpError, Task>);
            });

        var matchAsyncTaskValueFold = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tResult = typeArguments[1];
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(tResult) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(tResult)) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), TaskOf(tResult));
            });

        var matchAsyncTaskValueAction = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(Task) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, typeof(Task)) &&
                    parameters[2].ParameterType == typeof(Func<OpError, Task>);
            });

        Assert.NotNull(matchVoidFold);
        Assert.NotNull(matchVoidAction);
        Assert.NotNull(matchValueFold);
        Assert.NotNull(matchValueAction);
        Assert.NotNull(matchAsyncVoidFold);
        Assert.NotNull(matchAsyncVoidAction);
        Assert.NotNull(matchAsyncValueFold);
        Assert.NotNull(matchAsyncValueAction);
        Assert.NotNull(matchAsyncTaskVoidFold);
        Assert.NotNull(matchAsyncTaskVoidAction);
        Assert.NotNull(matchAsyncTaskValueFold);
        Assert.NotNull(matchAsyncTaskValueAction);
    }

    [Fact]
    public async Task MatchAndMatchAsync_BranchBehaviors_MatchSpec()
    {
        var matchVoidFoldDefinition = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var tResult = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == tResult &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    IsFuncOfReturn(parameters[1].ParameterType, tResult) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), tResult);
            });

        var matchVoidAction = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                    parameters[0].ParameterType == typeof(OpResult) &&
                    parameters[1].ParameterType == typeof(Action) &&
                    parameters[2].ParameterType == typeof(Action<OpError>);
            });

        var matchValueFoldDefinition = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tResult = typeArguments[1];
                var parameters = method.GetParameters();
                return method.ReturnType == tResult &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, tResult) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), tResult);
            });

        var matchValueActionDefinition = FindWorkflowMethod(
            name: "Match",
            parameterCount: 3,
            genericArity: 1,
            predicate: method =>
            {
                var t = method.GetGenericArguments().Single();
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(void) &&
                    parameters[0].ParameterType == OpResultOf(t) &&
                    IsActionOf(parameters[1].ParameterType, t) &&
                    parameters[2].ParameterType == typeof(Action<OpError>);
            });

        var matchAsyncTaskVoidAction = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 0,
            predicate: method =>
            {
                var parameters = method.GetParameters();
                return method.ReturnType == typeof(Task) &&
                    parameters[0].ParameterType == TaskOf(typeof(OpResult)) &&
                    parameters[1].ParameterType == typeof(Func<Task>) &&
                    parameters[2].ParameterType == typeof(Func<OpError, Task>);
            });

        var matchAsyncTaskValueFoldDefinition = FindWorkflowMethod(
            name: "MatchAsync",
            parameterCount: 3,
            genericArity: 2,
            predicate: method =>
            {
                var typeArguments = method.GetGenericArguments();
                var t = typeArguments[0];
                var tResult = typeArguments[1];
                var parameters = method.GetParameters();
                return method.ReturnType == TaskOf(tResult) &&
                    parameters[0].ParameterType == TaskOf(OpResultOf(t)) &&
                    IsFuncOfInputAndReturn(parameters[1].ParameterType, t, TaskOf(tResult)) &&
                    IsFuncOfInputAndReturn(parameters[2].ParameterType, typeof(OpError), TaskOf(tResult));
            });

        var matchVoidFold = matchVoidFoldDefinition.MakeGenericMethod(typeof(string));
        Func<string> onOkVoidFold = () => "ok-void";
        Func<OpError, string> onErrVoidFold = error => $"err-{error.Message}";

        var matchVoidFoldOk = InvokeWorkflow<string>(matchVoidFold, OpResults.Ok(), onOkVoidFold, onErrVoidFold);
        Assert.Equal("ok-void", matchVoidFoldOk);

        OpResult matchVoidFoldErrInput = OpResults.Err("v");
        var matchVoidFoldErr = InvokeWorkflow<string>(matchVoidFold, matchVoidFoldErrInput, onOkVoidFold, onErrVoidFold);
        Assert.Equal("err-v", matchVoidFoldErr);

        var matchVoidOnOkCalled = 0;
        var matchVoidOnErrCalled = 0;
        Action onOkVoidAction = () => matchVoidOnOkCalled++;
        Action<OpError> onErrVoidAction = _ => matchVoidOnErrCalled++;

        InvokeWorkflow(matchVoidAction, OpResults.Ok(), onOkVoidAction, onErrVoidAction);
        Assert.Equal(1, matchVoidOnOkCalled);
        Assert.Equal(0, matchVoidOnErrCalled);

        OpResult matchVoidActionErrInput = OpResults.Err("void-action");
        InvokeWorkflow(matchVoidAction, matchVoidActionErrInput, onOkVoidAction, onErrVoidAction);
        Assert.Equal(1, matchVoidOnOkCalled);
        Assert.Equal(1, matchVoidOnErrCalled);

        var matchValueFold = matchValueFoldDefinition.MakeGenericMethod(typeof(int), typeof(string));
        Func<int, string> onOkValueFold = value => $"ok-{value}";
        Func<OpError, string> onErrValueFold = error => $"err-{error.Message}";

        var matchValueFoldOk = InvokeWorkflow<string>(matchValueFold, OpResults.Ok(3), onOkValueFold, onErrValueFold);
        Assert.Equal("ok-3", matchValueFoldOk);

        var matchValueFoldErr = InvokeWorkflow<string>(matchValueFold, OpResults.Err<int>("value-fold"), onOkValueFold, onErrValueFold);
        Assert.Equal("err-value-fold", matchValueFoldErr);

        var matchValueAction = matchValueActionDefinition.MakeGenericMethod(typeof(int));
        var matchValueOnOkCalled = 0;
        var matchValueOnErrCalled = 0;
        Action<int> onOkValueAction = _ => matchValueOnOkCalled++;
        Action<OpError> onErrValueAction = _ => matchValueOnErrCalled++;

        InvokeWorkflow(matchValueAction, OpResults.Ok(41), onOkValueAction, onErrValueAction);
        Assert.Equal(1, matchValueOnOkCalled);
        Assert.Equal(0, matchValueOnErrCalled);

        InvokeWorkflow(matchValueAction, OpResults.Err<int>("value-action"), onOkValueAction, onErrValueAction);
        Assert.Equal(1, matchValueOnOkCalled);
        Assert.Equal(1, matchValueOnErrCalled);

        var matchAsyncTaskVoidOnOkCalled = 0;
        var matchAsyncTaskVoidOnErrCalled = 0;
        Func<Task> onOkAsyncVoidAction = () =>
        {
            matchAsyncTaskVoidOnOkCalled++;
            return Task.CompletedTask;
        };
        Func<OpError, Task> onErrAsyncVoidAction = _ =>
        {
            matchAsyncTaskVoidOnErrCalled++;
            return Task.CompletedTask;
        };

        await InvokeWorkflowAsync(
            matchAsyncTaskVoidAction,
            Task.FromResult(OpResults.Ok()),
            onOkAsyncVoidAction,
            onErrAsyncVoidAction);

        Assert.Equal(1, matchAsyncTaskVoidOnOkCalled);
        Assert.Equal(0, matchAsyncTaskVoidOnErrCalled);

        await InvokeWorkflowAsync(
            matchAsyncTaskVoidAction,
            Task.FromResult<OpResult>(OpResults.Err("matchasync-void")),
            onOkAsyncVoidAction,
            onErrAsyncVoidAction);

        Assert.Equal(1, matchAsyncTaskVoidOnOkCalled);
        Assert.Equal(1, matchAsyncTaskVoidOnErrCalled);

        var matchAsyncTaskValueFold = matchAsyncTaskValueFoldDefinition.MakeGenericMethod(typeof(int), typeof(string));
        Func<int, Task<string>> onOkAsyncValueFold = value => Task.FromResult($"ok-{value + 1}");
        Func<OpError, Task<string>> onErrAsyncValueFold = error => Task.FromResult($"err-{error.Message}");

        var matchAsyncTaskValueFoldOk = await InvokeWorkflowAsync<string>(
            matchAsyncTaskValueFold,
            Task.FromResult(OpResults.Ok(9)),
            onOkAsyncValueFold,
            onErrAsyncValueFold);

        Assert.Equal("ok-10", matchAsyncTaskValueFoldOk);

        var matchAsyncTaskValueFoldErr = await InvokeWorkflowAsync<string>(
            matchAsyncTaskValueFold,
            Task.FromResult(OpResults.Err<int>("matchasync-value")),
            onOkAsyncValueFold,
            onErrAsyncValueFold);

        Assert.Equal("err-matchasync-value", matchAsyncTaskValueFoldErr);
    }

    private static MethodInfo FindWorkflowMethod(
        string name,
        int parameterCount,
        int genericArity,
        Func<MethodInfo, bool> predicate)
    {
        var method = typeof(OpResultExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(candidate =>
                candidate.Name == name &&
                candidate.GetParameters().Length == parameterCount &&
                candidate.GetGenericArguments().Length == genericArity &&
                predicate(candidate))
            .SingleOrDefault();

        Assert.NotNull(method);
        return method!;
    }

    private static T InvokeWorkflow<T>(MethodInfo method, params object?[] args)
    {
        var value = method.Invoke(null, args);
        Assert.NotNull(value);
        return Assert.IsType<T>(value);
    }

    private static void InvokeWorkflow(MethodInfo method, params object?[] args)
    {
        _ = method.Invoke(null, args);
    }

    private static async Task<T> InvokeWorkflowAsync<T>(MethodInfo method, params object?[] args)
    {
        var taskObject = method.Invoke(null, args);
        var task = Assert.IsAssignableFrom<Task>(taskObject);
        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(resultProperty);
        var value = resultProperty!.GetValue(task);
        Assert.NotNull(value);
        return Assert.IsType<T>(value);
    }

    private static async Task InvokeWorkflowAsync(MethodInfo method, params object?[] args)
    {
        var taskObject = method.Invoke(null, args);
        var task = Assert.IsAssignableFrom<Task>(taskObject);
        await task.ConfigureAwait(false);
    }

    private static Type OpResultOf(Type valueType) => typeof(OpResult<>).MakeGenericType(valueType);

    private static Type TaskOf(Type valueType) => typeof(Task<>).MakeGenericType(valueType);

    private static bool IsFuncOfReturn(Type candidate, Type returnType) =>
        candidate.IsGenericType &&
        candidate.GetGenericTypeDefinition() == typeof(Func<>) &&
        candidate.GetGenericArguments()[0] == returnType;

    private static bool IsFuncOfInputAndReturn(Type candidate, Type inputType, Type returnType) =>
        candidate.IsGenericType &&
        candidate.GetGenericTypeDefinition() == typeof(Func<,>) &&
        candidate.GetGenericArguments()[0] == inputType &&
        candidate.GetGenericArguments()[1] == returnType;

    private static bool IsActionOf(Type candidate, Type parameterType) =>
        candidate.IsGenericType &&
        candidate.GetGenericTypeDefinition() == typeof(Action<>) &&
        candidate.GetGenericArguments()[0] == parameterType;
}