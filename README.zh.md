# OpResult

OpResult 是一个轻量的 .NET Result Pattern 类库，用显式的 `Ok` 和 `Err` 两条路径表达业务操作结果，避免把可预期的业务失败建模为异常。

## 核心类型

当前版本包含两个核心类型：

- `OpResult<T>`：成功时携带 `T` 值，失败时携带 `OpError`。
- `OpError`：固定错误类型，暴露 `Message`。

## 创建结果

成功结果可以直接从值返回，也可以通过 `OpResults.Ok(...)` 显式创建：

```csharp
OpResult<int> GetCount()
{
    return 42;
}

OpResult<int> GetExplicitCount()
{
    return OpResults.Ok(42);
}
```

失败结果通过 `OpResults.Err(...)` 创建。返回的 `OpError` 可以转换为任意 `OpResult<T>`，因此错误返回可以保持简洁：

```csharp
OpResult<int> GetCount()
{
    return OpResults.Err("count failed");
}
```

成功值遵循 non-null 契约。启用 nullable 的调用方向 `OpResults.Ok(...)` 传入 null，或把 nullable 引用直接转换成 `OpResult<T>` 时，应得到编译期警告。如果调用方显式绕过该警告，Ok 构造路径会抛出 `ArgumentNullException`，而不是创建携带 null 载荷的成功结果。

传给 `OpResults.Err(...)` 的 null 或空白错误消息会归一化为空消息。

## 读取分支

使用 `IsOk` 和 `IsErr` 检查当前分支，再消费 `Value` 或 `Error`：

```csharp
OpResult<string> result = LoadName();

if (result.IsOk)
{
    Console.WriteLine(result.Value);
}

if (result.IsErr)
{
    Console.WriteLine(result.Error.Message);
}
```

从失败结果读取 `Value` 会返回成功类型的默认值。从成功结果读取 `Error` 会返回一条空消息错误。`default(OpResult<T>)` 是错误消息为空的失败结果。

## 链式组合

使用 `Then` 继续执行另一个可能失败的操作。失败结果会短路，并传播当前错误：

```csharp
OpResult<Order> CreateOrder(Guid userId)
{
    return LoadUser(userId)
        .Then(ValidateUser)
        .Then(BuildOrder);
}
```

异步工作流使用 `Task<OpResult<T>>`，并通过 `ThenAsync` 组合：

```csharp
Task<OpResult<Order>> CreateOrderAsync(Guid userId)
{
    return LoadUserAsync(userId)
        .ThenAsync(ValidateUserAsync)
        .ThenAsync(BuildOrderAsync);
}
```

## 分支副作用

使用 `OnOk` 和 `OnOkAsync` 在成功分支执行副作用。使用 `OnErr` 和 `OnErrAsync` 在失败分支执行副作用。这些方法都会返回原结果。

```csharp
return await LoadUserAsync(id)
    .OnOkAsync(user => AuditAsync($"loaded {user.Id}"))
    .OnErrAsync(error => LogAsync(error.Message));
```

## 消费结果

当两个分支都要产生同一个返回值，或只运行匹配分支的副作用时，使用 `Match`：

```csharp
var text = result.Match(
    value => $"ok: {value}",
    error => $"err: {error.Message}");
```

异步分支处理使用 `MatchAsync`：

```csharp
await result.MatchAsync(
    value => WriteOkAsync(value),
    error => WriteErrAsync(error.Message));
```

## v0.1.0 范围

当前版本有意保持模型很小：一个结果类型、一个固定错误类型、直接分支属性，以及基于 Task 的工作流辅助方法。
