# TryInvoke Exception Adapters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 OpResult v0.1.0 实现 `OpResults.TryInvoke` / `TryInvokeAsync` 四个异常边界适配入口，并补齐测试与文档。

**Architecture:** `TryInvoke` 直接放在 `OpResults` 静态工厂类型中，保持四个零参数委托入口。测试集中新增一个 TryInvoke 专用测试文件，README 双语补充使用方式；不修改核心 Result spec。

**Tech Stack:** C# / .NET `net10.0;net6.0` library, xUnit v3 MTP tests, nullable enabled, XML docs enabled.

---

## 文件结构

- Create: `OpResult.Tests/TryInvokeTests.cs`，集中测试四个 TryInvoke 入口的公开签名和行为。
- Modify: `OpResult/OpResults.cs`，新增四个公开方法和 XML docs。
- Modify: `README.md`、`README.zh.md`，新增 TryInvoke 使用说明，移除旧的“TryInvoke 不属于当前核心 API”表述。
- Reference: `docs/superpowers/specs/2026-05-22-tryinvoke-exception-adapters-design.md` 是本任务唯一 TryInvoke 行为标准。

禁止 git 写操作：不要 `git add`、`git commit`、`git checkout`、`git reset`、`git branch` 写操作。每个任务完成后只运行 `git status --short` 作为状态检查。

### Task 1: TryInvoke 测试先行

**Files:**
- Create: `OpResult.Tests/TryInvokeTests.cs`

- [ ] **Step 1: 新增失败测试文件**

Create `OpResult.Tests/TryInvokeTests.cs` with:

```csharp
using System.Reflection;

namespace OpResult.Tests;

public sealed class TryInvokeTests
{
    [Fact]
    public void TryInvoke_SurfaceMatchesSpec()
    {
        var tryInvokeAction = FindMethod(
            nameof(OpResults.TryInvoke),
            typeof(OpResult),
            typeof(Action));

        var tryInvokeFunc = FindGenericMethod(
            nameof(OpResults.TryInvoke),
            typeof(OpResult<>),
            typeof(Func<>));

        var tryInvokeAsyncAction = FindMethod(
            nameof(OpResults.TryInvokeAsync),
            typeof(Task<OpResult>),
            typeof(Func<Task>));

        var tryInvokeAsyncFunc = FindGenericMethod(
            nameof(OpResults.TryInvokeAsync),
            typeof(Task<>),
            typeof(Func<>));

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

        Assert.True(actionResult.IsErr);
        Assert.Equal("async action failed", actionResult.Error!.Message);
        Assert.True(funcResult.IsErr);
        Assert.Equal("async func failed", funcResult.Error!.Message);
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

    private static MethodInfo FindGenericMethod(string name, Type returnTypeDefinition, Type parameterTypeDefinition) =>
        typeof(OpResults)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
            {
                if (method.Name != name || !method.IsGenericMethodDefinition)
                {
                    return false;
                }

                var parameters = method.GetParameters();
                return parameters.Length == 1
                    && method.ReturnType.IsGenericType
                    && method.ReturnType.GetGenericTypeDefinition() == returnTypeDefinition
                    && parameters[0].ParameterType.IsGenericType
                    && parameters[0].ParameterType.GetGenericTypeDefinition() == parameterTypeDefinition;
            });
}
```

- [ ] **Step 2: 运行失败测试**

Run:

```bash
dotnet build OpResult.slnx -c Release
```

Expected: FAIL because `OpResults.TryInvoke` / `TryInvokeAsync` do not exist yet.

### Task 2: 实现 TryInvoke API

**Files:**
- Modify: `OpResult/OpResults.cs`
- Test: `OpResult.Tests/TryInvokeTests.cs`

- [ ] **Step 1: 在 `OpResults` 中新增实现**

Modify `OpResult/OpResults.cs` by adding this private constant and four public methods inside `public static class OpResults`, after existing `Err<T>`:

```csharp
    private const string NullOperationResultMessage = "Operation returned null.";

    /// <summary>
    /// Invokes an action and converts non-cancellation exceptions to a failed result.
    /// </summary>
    /// <param name="action">The action to invoke.</param>
    /// <returns>A successful result when the action completes; otherwise, a failed result with the exception message.</returns>
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
            return Err(exception.Message);
        }
    }

    /// <summary>
    /// Invokes a function and converts non-cancellation exceptions or null return values to a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <returns>A successful result when the function returns a non-null value; otherwise, a failed result.</returns>
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
            return Err<T>(exception.Message);
        }
    }

    /// <summary>
    /// Invokes an asynchronous action and converts non-cancellation exceptions or null tasks to a failed result.
    /// </summary>
    /// <param name="action">The asynchronous action to invoke.</param>
    /// <returns>A successful result when the action task completes; otherwise, a failed result.</returns>
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
            return Err(exception.Message);
        }
    }

    /// <summary>
    /// Invokes an asynchronous function and converts non-cancellation exceptions, null tasks, or null return values to a failed result.
    /// </summary>
    /// <typeparam name="T">The type of the value carried by a successful result.</typeparam>
    /// <param name="func">The asynchronous function to invoke.</param>
    /// <returns>A successful result when the function task returns a non-null value; otherwise, a failed result.</returns>
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
            return Err<T>(exception.Message);
        }
    }
```

- [ ] **Step 2: 运行 TryInvoke 测试**

Run:

```bash
dotnet build OpResult.slnx -c Release
dotnet run --project OpResult.Tests -c Release --no-build
```

Expected: build succeeds and all tests pass.

- [ ] **Step 3: 检查状态**

Run:

```bash
git status --short
```

Expected: only planned files changed or created. Do not commit.

### Task 3: README 双语文档

**Files:**
- Modify: `README.md`
- Modify: `README.zh.md`

- [ ] **Step 1: 更新英文 README**

Modify `README.md`:

- Add a new section after the factory examples:

```markdown
## TryInvoke

Use `TryInvoke` when code at an exception boundary should be folded into `OpResult`:

```csharp
OpResult written = OpResults.TryInvoke(
    () => File.WriteAllText(path, text));

OpResult<User> loaded = OpResults.TryInvoke(
    () => repository.LoadUser(id));

OpResult saved = await OpResults.TryInvokeAsync(
    () => repository.SaveAsync(user, cancellationToken));

OpResult<User> fetched = await OpResults.TryInvokeAsync(
    () => repository.LoadUserAsync(id, cancellationToken));
```

Non-cancellation exceptions become `Err(exception.Message)`. Cancellation exceptions propagate. If an adapted value-returning operation returns `null`, or an async operation returns a `null` task, the result is `Err("Operation returned null.")`.

Use lambdas to pass arguments or cancellation tokens to the adapted operation.
```

- Replace the old v0.1.0 scope bullet:

```markdown
- `TryInvoke` is not part of the current core API.
```

with:

```markdown
- `TryInvoke` covers exception-boundary adapters for `Action`, `Func<T>`, `Func<Task>`, and `Func<Task<T>>`.
```

- [ ] **Step 2: 更新中文 README**

Modify `README.zh.md`:

- Add a matching section after the factory examples:

```markdown
## TryInvoke

当异常边界代码需要折叠成 `OpResult` 时，使用 `TryInvoke`：

```csharp
OpResult written = OpResults.TryInvoke(
    () => File.WriteAllText(path, text));

OpResult<User> loaded = OpResults.TryInvoke(
    () => repository.LoadUser(id));

OpResult saved = await OpResults.TryInvokeAsync(
    () => repository.SaveAsync(user, cancellationToken));

OpResult<User> fetched = await OpResults.TryInvokeAsync(
    () => repository.LoadUserAsync(id, cancellationToken));
```

非取消异常会转成 `Err(exception.Message)`。取消异常会继续传播。若被适配的有值操作返回 `null`，或异步操作返回 null task，结果为 `Err("Operation returned null.")`。

需要传入参数或 cancellation token 时，使用 lambda / 闭包传给实际业务方法。
```

- Replace:

```markdown
- `TryInvoke` 不属于当前核心 API。
```

with:

```markdown
- `TryInvoke` 覆盖 `Action`、`Func<T>`、`Func<Task>` 与 `Func<Task<T>>` 的异常边界适配。
```

- [ ] **Step 3: 运行验证**

Run:

```bash
dotnet build OpResult.slnx -c Release
dotnet run --project OpResult.Tests -c Release --no-build
```

Expected: build succeeds and all tests pass.

- [ ] **Step 4: 检查状态**

Run:

```bash
git status --short
```

Expected: spec, plan, source, tests, and README files changed. Do not commit.

### Task 4: 最终自审与验证

**Files:**
- Review: `docs/superpowers/specs/2026-05-22-tryinvoke-exception-adapters-design.md`
- Review: `docs/superpowers/plans/2026-05-22-tryinvoke-exception-adapters.md`
- Review: `OpResult/OpResults.cs`
- Review: `OpResult.Tests/TryInvokeTests.cs`
- Review: `README.md`
- Review: `README.zh.md`

- [ ] **Step 1: 检查 spec 覆盖**

Confirm every acceptance bullet in `docs/superpowers/specs/2026-05-22-tryinvoke-exception-adapters-design.md` has a corresponding test in `OpResult.Tests/TryInvokeTests.cs`.

- [ ] **Step 2: 检查公开 API 面**

Confirm no additional public TryInvoke overloads were added beyond:

```csharp
TryInvoke(Action)
TryInvoke<T>(Func<T>)
TryInvokeAsync(Func<Task>)
TryInvokeAsync<T>(Func<Task<T>>)
```

- [ ] **Step 3: 运行最终验证**

Run:

```bash
dotnet build OpResult.slnx -c Release
dotnet run --project OpResult.Tests -c Release --no-build
```

Expected: build succeeds and all tests pass.

- [ ] **Step 4: 检查最终状态**

Run:

```bash
git status --short
```

Expected: no unrelated files changed. Do not commit.

## Plan Self-Review

- Spec coverage: all public API, null, exception, cancellation, docs, and verification requirements are mapped to tasks.
- Placeholder scan: no TBD/TODO placeholders remain.
- Type consistency: method names, parameter names, return types, and fixed message match the TryInvoke spec.
