# OpResult v1 API 设计基线

## 设计目标

OpResult 是一个轻量的 .NET Result Pattern 类库，用于表达业务操作的 Ok / Err 两条路径。API 优先服务业务工作流可读性，同时利用现代 C# nullable flow analysis 在编译期提示空值风险。

## 核心模型

v1 只实现 `OpResult<T>`。

`OpResult<T>` 表示一个成功值类型为 `T` 的结果，错误类型固定为 `OpError`。

Ok payload 和 Err payload 默认按 non-null 设计。`IsOk` 成立时，`Value` 可作为成功值使用；`IsErr` 成立时，`Error` 可作为错误值使用。

`OpError` 是 Basic Result 的唯一错误类型，同时也是 `OpResults.Err(...)` 返回的错误值。`OpError` 使用 `public sealed record class`，只包含只读 `Message`，不使用 positional record，不公开构造器。

错误创建统一通过 `OpResults.Err("message")` 表达。`OpError` 支持隐式转换为任意 `OpResult<T>`，用于错误早返回：

```csharp
OpResult<int> GetCount()
{
    return OpResults.Err("count failed");
}
```

选择 `sealed record class` 是为了保护长期 Ok path 性能。v2 预计会扩展错误模型；引用类型可以让 `OpResult<T>` 只持有一个错误引用，避免 `OpError` 增加字段后膨胀每个 `OpResult<T>` 实例。

## 核心属性

`OpResult<T>` 只承载状态和值：

- `IsOk`
- `IsErr`
- `Value`
- `Error`

`Value` / `Error` 是只读属性，公开签名采用：

```csharp
public T? Value { get; }

public OpError? Error { get; }
```

`T?` 是无约束泛型上的 nullable annotation。`T` 为 `int` 等值类型时，消费体验仍是直接读取值，不需要 `.Value.Value`：

```csharp
OpResult<int> result = OpResults.Ok(42);

if (result.IsOk)
{
    int value = result.Value;
}
```

状态属性通过 nullable attributes 绑定分支载荷：

```csharp
[MemberNotNullWhen(true, nameof(Value))]
public bool IsOk { get; }

[MemberNotNullWhen(true, nameof(Error))]
public bool IsErr { get; }
```

目标使用体验是：

```csharp
if (result.IsOk)
{
    Use(result.Value);
}

if (result.IsErr)
{
    Log(result.Error);
}
```

未通过 `IsOk` / `IsErr` 验证就访问对应属性时，IDE/LSP 应尽量给出 nullable warning；验证对应状态后，应能顺滑消费对应属性。

`Value` / `Error` getter 自身不因状态不匹配主动抛异常。Err 时读取 `Value` 返回 `default` 语义；如果调用方继续解引用该默认值，仍可能触发普通 C# 空引用行为。Ok 时读取 `Error` 返回默认空消息错误对象，保证 `result.Error.Message` 不因错误不存在而抛空引用。

`default(OpResult<T>)` 定义为 Err 空消息：

```csharp
default(OpResult<int>).IsErr == true;
default(OpResult<int>).Error.Message == string.Empty;
```

## 构造方式

成功路径支持直接返回成功值：

```csharp
OpResult<int> GetCount()
{
    return 42;
}
```

也可以用 `OpResults.Ok(...)` 显式构造 Ok：

```csharp
var result = OpResults.Ok(42);
```

错误路径通过 `OpResults.Err(...)` 显式表达：

```csharp
OpResult<int> GetCount()
{
    return OpResults.Err("count failed");
}
```

`OpResults.Err(...)` 返回 `OpError`。当目标类型是 `OpResult<T>` 时，`OpError` 通过隐式转换进入 Err 分支。

`OpResults.Err(null)` 和空白 message 在运行时归一化为 `string.Empty`，不抛异常。公开 API 仍使用 nullable 标注表达 non-null 意图，让 IDE/LSP 在编译期尽量提示错误调用。

`OpResults.Ok(...)` 和成功值隐式转换也按 non-null 设计。引用类型传入 null 时依赖 nullable 标注给出编译期提示；如果调用方通过 `null!` 等方式显式绕过提示，运行时在 Ok 构造边界抛出 `ArgumentNullException`，不创建 Ok(null)。

## 工作流组合

业务工作流通过扩展方法表达：

- `Then`
- `ThenAsync`
- `OnOk`
- `OnOkAsync`
- `OnErr`
- `OnErrAsync`
- `Match`
- `MatchAsync`

`Then` 表示 Ok 后继续一个同步且可能失败的步骤。

`ThenAsync` 表示 Ok 后继续一个异步且可能失败的步骤。

```csharp
return await LoadUserAsync(id)
    .ThenAsync(ValidateUserAsync)
    .ThenAsync(CreateOrderAsync);
```

`OnOk` / `OnOkAsync` 用于在 Ok 分支执行副作用，并保持原 Result 不变。

`OnErr` / `OnErrAsync` 用于在 Err 分支执行副作用，并保持原 Result 不变。

`Match` / `MatchAsync` 用于消费 Ok / Err 两个分支，支持返回值分支和副作用分支。

扩展方法签名以 `Task` 为异步标准，不引入 `ValueTask`。扩展方法本身不显式接收 `CancellationToken`；需要取消时，由调用方在业务方法参数或委托闭包中表达。

基础签名标准如下：

```csharp
public static OpResult<TNext> Then<T, TNext>(
    this OpResult<T> result,
    Func<T, OpResult<TNext>> onOk);

public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
    this OpResult<T> result,
    Func<T, Task<OpResult<TNext>>> onOk);

public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task<OpResult<TNext>>> onOk);

public static OpResult<T> OnOk<T>(
    this OpResult<T> result,
    Action<T> onOk);

public static Task<OpResult<T>> OnOkAsync<T>(
    this OpResult<T> result,
    Func<T, Task> onOk);

public static Task<OpResult<T>> OnOkAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task> onOk);

public static OpResult<T> OnErr<T>(
    this OpResult<T> result,
    Action<OpError> onErr);

public static Task<OpResult<T>> OnErrAsync<T>(
    this OpResult<T> result,
    Func<OpError, Task> onErr);

public static Task<OpResult<T>> OnErrAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<OpError, Task> onErr);

public static TResult Match<T, TResult>(
    this OpResult<T> result,
    Func<T, TResult> onOk,
    Func<OpError, TResult> onErr);

public static void Match<T>(
    this OpResult<T> result,
    Action<T> onOk,
    Action<OpError> onErr);
```

`MatchAsync` 对应支持返回值分支和副作用分支，并支持 `OpResult<T>` receiver 与 `Task<OpResult<T>>` receiver：

```csharp
public static Task<TResult> MatchAsync<T, TResult>(
    this OpResult<T> result,
    Func<T, Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr);

public static Task<TResult> MatchAsync<T, TResult>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr);

public static Task MatchAsync<T>(
    this OpResult<T> result,
    Func<T, Task> onOk,
    Func<OpError, Task> onErr);

public static Task MatchAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task> onOk,
    Func<OpError, Task> onErr);
```

`OnOk` / `OnErr` 只执行副作用，并保持原 Result 不变。

`MapOk` / `MapErr` 不作为当前已敲定的 v1 API 写入。

## 异步模型

异步 Result 使用 .NET 原生形态：

```csharp
Task<OpResult<T>>
```

扩展方法负责补齐链式组合体验，不引入额外 async result 类型。

## 性能原则

`OpResult<T>` 保持值类型容器，优先保护 Ok path。

`OpError` 使用 `sealed record class`，接受 Err path 上的小对象分配，以换取错误模型扩展时 `OpResult<T>` 尺寸稳定。未来 `OpError` 增加 `Code`、`Cause`、`Metadata` 或错误链时，扩展成本应停留在 Err 对象上，而不是让每个 Ok result 都携带额外字段。

运行时策略优先避免 Result API 在已构造的 Result 流程中抛出空引用异常。无效 error message、错误分支访问和默认值都通过可用默认值兜底；非法 null 成功值在 Ok 构造边界抛出明确的 `ArgumentNullException`，避免坏状态进入 Ok path。正确消费方式由 `IsOk` / `IsErr`、nullable 标注和文档约定共同表达。

## 命名原则

API 命名优先降低业务开发者心智负担。

链式短路使用 `Then` / `ThenAsync` 表达业务步骤串联。

分支副作用使用 `OnOk` / `OnErr` 表达。

双分支消费统一使用 `Match` / `MatchAsync`。
