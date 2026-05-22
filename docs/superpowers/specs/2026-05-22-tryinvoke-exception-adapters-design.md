# TryInvoke 异常边界适配器设计

## 设计目标

`TryInvoke` 为 OpResult v0.1.0 提供轻量的异常边界适配能力：调用方可以把可能抛异常、返回 null 或来自旧代码边界的委托折叠成 `OpResult` / `OpResult<T>`。

该能力独立于现有核心 Result API spec 记录，但入口暂时放在 `OpResults` 下，作为本库的工厂能力之一。若后续 mapper、filter、错误码或更多委托形态导致工厂面膨胀，再单独评估是否拆分到独立类型。

v0.1.0 不引入自定义异常映射器，不保留原始 `Exception` 对象，不新增错误码、错误类型或 metadata。

## 公开 API

v0.1.0 只提供 sync / async 与 void-like / value 四个零参数委托入口：

```csharp
public static OpResult TryInvoke(Action action);

public static OpResult<T> TryInvoke<T>(Func<T> func)
    where T : notnull;

public static Task<OpResult> TryInvokeAsync(Func<Task> action);

public static Task<OpResult<T>> TryInvokeAsync<T>(Func<Task<T>> func)
    where T : notnull;
```

参数命名按业务语义区分：

- `action` 表示成功时没有业务值，包括 `Action` 与 `Func<Task>`。
- `func` 表示成功时有业务值，包括 `Func<T>` 与 `Func<Task<T>>`。

v0.1.0 不提供以下入口：

- 显式 `CancellationToken` 重载；需要取消时由调用方通过 lambda / 闭包传给实际业务方法。
- 带参数委托重载；需要参数时由调用方通过 lambda / 闭包表达。
- `ValueTask` 重载。
- 已创建 `Task` / `Task<T>` 的直接包装 API。
- 本身返回 `OpResult` / `OpResult<T>` 的委托保护 API。
- exception mapper / filter 重载。

## 行为规则

`TryInvoke(Action action)`：

- `action` 为 null 时抛 `ArgumentNullException`。
- `action` 成功执行完成时返回 `OpResults.Ok()`。
- `action` 抛出 `OperationCanceledException` 或其派生类型时传播原异常，不转 Err。
- `action` 抛出其他 `Exception` 时返回 `OpResults.Err(exception.Message)`。

`TryInvoke<T>(Func<T> func)`：

- `func` 为 null 时抛 `ArgumentNullException`。
- `func` 返回 non-null 值时返回 `OpResults.Ok(value)`。
- `func` 返回 null 时返回 `OpResults.Err<T>("Operation returned null.")`。
- `func` 抛出 `OperationCanceledException` 或其派生类型时传播原异常，不转 Err。
- `func` 抛出其他 `Exception` 时返回 `OpResults.Err<T>(exception.Message)`。

`TryInvokeAsync(Func<Task> action)`：

- `action` 为 null 时抛 `ArgumentNullException`。
- `action` 返回 null task 时返回 `OpResults.Err("Operation returned null.")`。
- task 成功完成时返回 `OpResults.Ok()`。
- 调用 `action` 或 await task 期间抛出 `OperationCanceledException` 或其派生类型时传播原异常，不转 Err。
- 调用 `action` 或 await task 期间抛出其他 `Exception` 时返回 `OpResults.Err(exception.Message)`。

`TryInvokeAsync<T>(Func<Task<T>> func)`：

- `func` 为 null 时抛 `ArgumentNullException`。
- `func` 返回 null task 时返回 `OpResults.Err<T>("Operation returned null.")`。
- task 成功完成并得到 non-null 值时返回 `OpResults.Ok(value)`。
- task 成功完成但得到 null 值时返回 `OpResults.Err<T>("Operation returned null.")`。
- 调用 `func` 或 await task 期间抛出 `OperationCanceledException` 或其派生类型时传播原异常，不转 Err。
- 调用 `func` 或 await task 期间抛出其他 `Exception` 时返回 `OpResults.Err<T>(exception.Message)`。

普通异常转 Err 时只使用原始 `exception.Message`，不添加包装文案，不做固定兜底。若消息为 null、空字符串或空白字符串，沿用现有 `OpError` 的归一化行为。

`"Operation returned null."` 是 v0.1.0 的固定错误消息，用于 null task 和 null payload 这类没有原始异常消息的适配失败。

## 与核心 Result 契约的关系

`OpResults.Ok<T>(null!)` 仍按核心 Result 契约抛 `ArgumentNullException`，不创建 Ok(null)。

`TryInvoke<T>` / `TryInvokeAsync<T>` 对 null 返回值采用 Err，是边界适配语义：被适配委托未能产生合法 Ok payload，因此返回失败结果。这不改变 `OpResult<T>` 成功值必须 non-null 的核心契约。

`OperationCanceledException` 和 `TaskCanceledException` 保持 .NET 原生取消语义，不能被记录为业务 Err。

## 文档要求

README 与 README.zh 应新增 TryInvoke 示例，并删除“TryInvoke 不属于当前核心 API”的旧表述。

文档应说明：

- `TryInvoke` / `TryInvokeAsync` 位于 `OpResults`。
- 普通异常使用 `exception.Message` 转 Err。
- null task / null payload 返回 `Err("Operation returned null.")`。
- 取消异常传播，不转 Err。
- 需要参数或 cancellation token 时使用 lambda / 闭包。

## 验收标准

测试至少覆盖：

- 四个公开重载存在，返回类型分别为 `OpResult`、`OpResult<T>`、`Task<OpResult>`、`Task<OpResult<T>>`。
- sync `action` 成功返回 Ok。
- sync `func` 成功返回 Ok(value)。
- async `action` 成功返回 Ok。
- async `func` 成功返回 Ok(value)。
- 四个入口的 null 委托参数抛 `ArgumentNullException`。
- sync / async 普通异常使用原始 `exception.Message` 转 Err。
- sync / async value 入口返回 null payload 时返回 `Err<T>("Operation returned null.")`。
- async void-like / value 入口返回 null task 时返回 Err，消息为 `"Operation returned null."`。
- sync / async 取消异常传播，不转 Err。

实现后必须通过：

```bash
dotnet build OpResult.slnx -c Release
dotnet run --project OpResult.Tests -c Release --no-build
```
