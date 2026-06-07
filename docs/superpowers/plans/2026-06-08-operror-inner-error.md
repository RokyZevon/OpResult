# OpError InnerError Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通过 `InnerError` 为 `OpError` 增加轻量结构化错误链，提供 `error.ToErr("...")` 包装人体工学，让 workflow API 保留错误链，并把异常映射成可一行显示的 `OpError` 链，同时不保存异常对象。

**Architecture:** `OpError` 继续作为唯一错误值类型。它新增可选直接内层错误 `InnerError`、BCL 风格一行 `ToString()`、一个非泛型 factory overload，以及一个用于包装的扩展方法。现有 result/workflow API 在短路时传播已有 `OpError` 引用，不再从 `Message` 重建。`TryInvoke` 使用内部 exception-to-error mapper，但不公开新的异常 adapter。

**Tech Stack:** C#/.NET、xUnit、Roslyn nullable-flow 编译测试、无反射/动态代码/源生成依赖的 AOT 兼容库代码。

---

## 参考

Spec: `docs/superpowers/specs/2026-06-08-operror-inner-error-design.md`

关键约束：

- 不新增公开 `FromException`。
- 不新增 `OpResults.Err<T>(string?, OpError?)`。
- 本版本不新增 `result.ToErr(...)`。
- 不在 `OpError` 内保存 `Exception`、stack trace、metadata、type/code 字段或字典。
- 不使用 git 写操作命令。仓库指令禁止 git 写操作，因此本计划刻意省略 commit 步骤。

## 任务清单

- [x] Task 1: 添加 OpError Chain 测试。
- [x] Task 2: 实现 OpError.InnerError、Factory、Extension 和 ToString。
- [x] Task 3: 添加 result.Error.ToErr 的 Nullable Flow 测试。
- [x] Task 4: 在 Then / ThenAsync 中保留 OpError 引用。
- [x] Task 5: 更新 TryInvoke 异常映射。
- [x] Task 6: 更新 README 文档。
- [x] Task 7: 执行最终验证。

## Task 1: 添加 OpError Chain 测试

文件：

- `OpResult.Tests/OpResultTests.cs`

先为直接 `InnerError`、factory 行为、`ToErr`、`ToString()` 和 null receiver guard 添加失败测试。

建议测试：

```csharp
[Fact]
public void Err_WithInnerErrorPreservesInnerError()
{
    var inner = OpResults.Err("user not found");

    var error = OpResults.Err("get user failed", inner);

    Assert.Equal("get user failed", error.Message);
    Assert.Same(inner, error.InnerError);
}

[Fact]
public void Err_WithNullInnerErrorCreatesSingleLayerError()
{
    var error = OpResults.Err("failed", innerError: null);

    Assert.Equal("failed", error.Message);
    Assert.Null(error.InnerError);
}

[Fact]
public void ToErr_WrapsReceiverAsInnerError()
{
    var inner = OpResults.Err("user not found");

    var error = inner.ToErr("get user failed");

    Assert.Equal("get user failed", error.Message);
    Assert.Same(inner, error.InnerError);
}

[Fact]
public void ToErr_WithNullReceiverThrows()
{
    var exception = Assert.Throws<ArgumentNullException>(() =>
        ((OpError)null!).ToErr("outer"));

    Assert.Equal("innerError", exception.ParamName);
}

[Fact]
public void ToString_ReturnsOuterToInnerChain()
{
    var error = OpResults.Err("database failed")
        .ToErr("get user failed")
        .ToErr("get profile failed");

    Assert.Equal("get profile failed -> get user failed -> database failed", error.ToString());
}

[Fact]
public void ToString_SkipsEmptyMessageNodes()
{
    var error = OpResults.Err("database failed")
        .ToErr("")
        .ToErr("get profile failed");

    Assert.Equal("get profile failed -> database failed", error.ToString());
}

[Fact]
public void ToString_ReturnsPlaceholderWhenAllMessagesAreEmpty()
{
    var error = OpResults.Err("")
        .ToErr(null)
        .ToErr(" ");

    Assert.Equal("<error>", error.ToString());
}
```

运行：

```bash
dotnet test
```

预期结果：

- 新测试失败，因为 `InnerError`、overload、extension 和 `ToString()` 尚未实现。

## Task 2: 实现 OpError.InnerError、Factory、Extension 和 ToString

文件：

- `OpResult/OpError.cs`
- `OpResult/OpResults.cs`
- 新增 `OpResult/OpErrorExtensions.cs`

更新 `OpError`，保存可选的直接内层错误。

实现草图：

```csharp
namespace OpResult;

public sealed record class OpError
{
    internal static OpError Empty { get; } = new(string.Empty);

    private OpError(string? message, OpError? innerError = null)
    {
        Message = message ?? string.Empty;
        InnerError = innerError;
    }

    public string Message { get; }

    public OpError? InnerError { get; }

    public override string ToString()
    {
        var current = this;
        var builder = new StringBuilder();

        while (current is not null)
        {
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                if (builder.Length > 0)
                    builder.Append(" -> ");

                builder.Append(current.Message);
            }

            current = current.InnerError;
        }

        return builder.Length == 0 ? "<error>" : builder.ToString();
    }

    internal static OpError New(string? message) =>
        string.IsNullOrWhiteSpace(message) ? Empty : new(message);

    internal static OpError New(string? message, OpError? innerError) =>
        innerError is null
            ? New(message)
            : new(string.IsNullOrWhiteSpace(message) ? string.Empty : message, innerError);
}
```

注意事项：

- 如果使用 `StringBuilder`，添加 `using System.Text;`。
- 保留 `Empty` 作为可复用的单层空错误。
- `New(message, innerError)` 在 `innerError` 非 null 时不能返回 `Empty`，因为即使外层消息为空，也必须保留链结构。

在 `OpResults` 中添加公开 factory overload：

```csharp
public static OpError Err(string? message, OpError? innerError) =>
    OpError.New(message, innerError);
```

添加扩展方法：

```csharp
namespace OpResult;

public static class OpErrorExtensions
{
    public static OpError ToErr(this OpError innerError, string? message)
    {
        ArgumentNullException.ThrowIfNull(innerError);

        return OpResults.Err(message, innerError);
    }
}
```

运行：

```bash
dotnet test
```

预期结果：

- Task 1 测试通过。
- 现有测试可能仍会在 workflow 或 `TryInvoke` 期望处失败，后续任务会更新。

## Task 3: 添加 result.Error.ToErr 的 Nullable Flow 测试

文件：

- `OpResult.Tests/NullableFlowCompilationTests.cs`

添加测试，证明 `result.Error.ToErr("...")` 可以被现有 `IsErr` nullable flow 守卫。

建议通过测试：

```csharp
[Fact]
public void ErrorToErrInsideIsErrBranchDoesNotWarn()
{
    const string source = """
        using OpResult;

        public static class NullableFlowScenario
        {
            public static OpResult<string> Wrap(OpResult<int> result)
            {
                if (result.IsErr)
                    return result.Error.ToErr("outer failed");

                return OpResults.Ok(result.Value.ToString());
            }
        }
        """;

    var diagnostics = CompileSnippet(source);

    Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CS8604");
}
```

建议警告测试：

```csharp
[Fact]
public void ErrorToErrWithoutIsErrBranchWarns()
{
    const string source = """
        using OpResult;

        public static class NullableFlowScenario
        {
            public static OpResult<string> Wrap(OpResult<int> result)
            {
                return result.Error.ToErr("outer failed");
            }
        }
        """;

    var diagnostics = CompileSnippet(source);

    Assert.Contains(diagnostics, diagnostic => diagnostic.Id == "CS8604");
}
```

运行：

```bash
dotnet test --filter NullableFlowCompilationTests
```

预期结果：

- 被 `IsErr` 守卫的用法不产生 CS8604。
- 未守卫用法产生 CS8604。

## Task 4: 在 Then / ThenAsync 中保留 OpError 引用

文件：

- `OpResult.Tests/WorkflowTests.cs`
- `OpResult/OpResultExtensions.cs`

先添加测试。短路路径必须断言 `Assert.Same(originalError, propagated.Error)`。

建议同步测试：

```csharp
[Fact]
public void Then_WhenSourceIsErrPreservesOriginalErrorReference()
{
    var originalError = OpResults.Err("database failed").ToErr("get user failed");
    var source = OpResult<int>.Err(originalError);

    var result = source.Then(value => OpResults.Ok(value.ToString()));

    Assert.True(result.IsErr);
    Assert.Same(originalError, result.Error);
}
```

建议异步测试：

```csharp
[Fact]
public async Task ThenAsync_WhenSourceIsErrPreservesOriginalErrorReference()
{
    var originalError = OpResults.Err("database failed").ToErr("get user failed");
    var source = OpResult<int>.Err(originalError);

    var result = await source.ThenAsync(value => Task.FromResult(OpResults.Ok(value.ToString())));

    Assert.True(result.IsErr);
    Assert.Same(originalError, result.Error);
}
```

实现方向：

- 替换这类短路路径：

  ```csharp
  return OpResults.Err(result.Error.Message);
  ```

  改用能保留 `result.Error` 的 overload 或 internal constructor。

- 优先使用已有 internal static 方法：

  ```csharp
  return OpResult.Err(result.Error);
  return OpResult<TNext>.Err(result.Error);
  ```

- 如果 nullable flow 已经证明非 null，避免使用 `result.Error!`；保持代码与当前 nullability 注解一致。

运行：

```bash
dotnet test
```

预期结果：

- Workflow 短路测试通过。
- 既有 workflow 行为不回退。

## Task 5: 更新 TryInvoke 异常映射

文件：

- `OpResult.Tests/TryInvokeTests.cs`
- `OpResult/OpResults.cs`

先添加或更新测试，再实现。

测试场景：

- 非泛型 `TryInvoke` 包含完整异常类型名和消息。
- 泛型 `TryInvoke<T>` 包含完整异常类型名和消息。
- inner exception 映射为 `InnerError` 链。
- 映射后的异常链通过 `ToString()` 输出外层到内层的一行显示。
- 空异常消息只映射为完整异常类型名。
- null task / null payload 仍精确返回 `"Operation returned null."`。
- cancellation exception 仍向外传播。

建议 inner exception 测试：

```csharp
[Fact]
public void TryInvoke_WhenExceptionHasInnerExceptionMapsInnerErrorChain()
{
    var result = OpResults.TryInvoke(() =>
        throw new InvalidOperationException(
            "outer failed",
            new ArgumentException("bad user id")));

    Assert.True(result.IsErr);
    Assert.Equal("System.InvalidOperationException: outer failed", result.Error.Message);
    Assert.NotNull(result.Error.InnerError);
    Assert.Equal("System.ArgumentException: bad user id", result.Error.InnerError.Message);
    Assert.Equal(
        "System.InvalidOperationException: outer failed -> System.ArgumentException: bad user id",
        result.Error.ToString());
}
```

建议空消息测试：

```csharp
[Fact]
public void TryInvoke_WhenExceptionMessageIsEmptyUsesExceptionTypeName()
{
    var result = OpResults.TryInvoke(() => throw new InvalidOperationException(""));

    Assert.True(result.IsErr);
    Assert.Equal("System.InvalidOperationException", result.Error.Message);
}
```

`OpResults` 中的实现草图：

```csharp
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
```

替换 exception catch body：

```csharp
catch (Exception exception) when (exception is not OperationCanceledException)
{
    return OpResult.Err(MapException(exception));
}
```

以及泛型等价路径：

```csharp
catch (Exception exception) when (exception is not OperationCanceledException)
{
    return OpResult<T>.Err(MapException(exception));
}
```

运行：

```bash
dotnet test --filter TryInvokeTests
dotnet test
```

预期结果：

- 更新后的 `TryInvoke` 期望通过。
- cancellation 传播保持不变。

## Task 6: 更新 README 文档

文件：

- `README.md`
- `README.zh.md`

保持两份文档一致更新。

需要说明：

- `OpError.InnerError`
- `error.ToErr("...")` 是推荐包装形态。
- 通过 `error.ToString()` 一行日志记录。
- `TryInvoke` 异常映射现在包含异常类型名和 inner exception 链。
- `ToString()` 只用于显示，不是可解析协议。

建议英文示例：

```csharp
var getUserResult = GetUser(1);
if (getUserResult.IsErr)
    return getUserResult.Error.ToErr("get profile failed");
```

建议中文示例：

```csharp
var getUserResult = GetUser(1);
if (getUserResult.IsErr)
    return getUserResult.Error.ToErr("获取用户资料失败");
```

一行日志示例：

```csharp
logger.LogError("{Error}", result.Error);
```

预期显示：

```text
get profile failed -> user not found
```

运行：

```bash
dotnet test
```

预期结果：

- 文档示例与已实现 API 概念匹配。
- 测试套件仍然通过。

## Task 7: 最终验证

运行：

```bash
dotnet format --verify-no-changes
dotnet test
dotnet pack -c Release
```

预期结果：

- `dotnet format --verify-no-changes` exits 0。
- `dotnet test` exits 0。
- `dotnet pack -c Release` exits 0，并在标准项目输出路径下生成 package。

同时手动检查公开 API 形态：

- `OpError.Message`
- `OpError.InnerError`
- `OpError.ToString()`
- `OpResults.Err(string? message)`
- `OpResults.Err(string? message, OpError? innerError)`
- `OpErrorExtensions.ToErr(this OpError innerError, string? message)`

确认不存在的 API：

- 没有公开 `FromException`。
- 没有 `OpResults.Err<T>(string?, OpError?)`。
- 没有 `result.ToErr(...)`。
- 没有 analyzer/source-generator package 或 project。
- `OpError` 上没有 `Exception`、metadata dictionary、error code 或 stack trace storage。
