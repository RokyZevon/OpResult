# OpResult v1 API 设计基线

## 设计目标

OpResult 是一个轻量的 .NET Result Pattern 类库，用于表达业务操作的 Ok / Err 两条路径。API 优先服务业务工作流可读性，同时利用现代 C# nullable flow analysis 在编译期提示引用类型空值风险。

v0.1.0 同时提供两个一等 Result 类型：

- `OpResult`：用于没有成功值的 void-like 业务操作。
- `OpResult<T>`：用于成功时携带 non-null `T` 值的业务操作。

异步主线使用 .NET 原生 `Task<OpResult>` / `Task<OpResult<T>>`，通过扩展方法补齐链式组合体验。不引入独立 async result 类型，不引入 `ValueTask`。

`TryInvoke` / 异常流适配器不属于本 spec。本 spec 只定义核心 Result 模型、工厂、分支消费、nullable flow 契约和文档同步要求。

## 核心模型

`OpResult` 表示一个成功时不携带值、失败时携带 `OpError` 的结果。

`OpResult<T>` 表示一个成功值类型为 `T` 的结果，错误类型固定为 `OpError`。`T` 必须满足 `notnull` 约束：

```csharp
public readonly record struct OpResult<T>
    where T : notnull
```

v0.1.0 的 Ok payload 按 non-null 设计。`OpResult<User?>` / `OpResults.Ok<User?>(null)` 不作为“成功但值可空”的受支持模型。需要表达“成功但业务值缺失”时，应在后续设计显式 optional / maybe 类型或使用业务类型建模，而不是把 nullable payload 混入 Result 状态。

`OpError` 是 v0.1.0 的唯一错误类型，使用 `public sealed record class`，只包含只读、non-null 的 `Message`。`Message` 为 null、空字符串或空白字符串时归一化为 `string.Empty`。`OpError` 不使用 positional record，不公开构造器。

`OpError` 可以在实现内部复用空消息错误实例，但 public API 不承诺引用身份。调用方不能依赖 `ReferenceEquals` 或 `Message` 是否为空来判断 Result 分支。

## 核心属性

`OpResult` 只承载状态和错误：

```csharp
public bool IsOk { get; }

public bool IsErr { get; }

public OpError? Error { get; }
```

`OpResult<T>` 承载状态、成功值和错误：

```csharp
public bool IsOk { get; }

public bool IsErr { get; }

public T? Value { get; }

public OpError? Error { get; }
```

`Value` / `Error` 的 nullable 签名用于表达“必须先验证分支再消费对应载荷”。运行时访问器不因分支不匹配抛异常：

- Err 分支读取 `Value` 返回 `default(T)`。
- Ok 分支读取 `Error` 返回空消息 `OpError`。

这些行为是运行时兜底，不是推荐消费路径。正确消费方式必须通过 `IsOk` / `IsErr` / `Then` / `Match` 等 API 表达。

分支判断只能使用 `IsOk` / `IsErr` / `Match`。不得使用以下方式判断分支：

```csharp
result.Value != null
result.Error != null
result.Error.Message == string.Empty
```

这些表达式都不是分支语义。尤其是 `Error` 在 Ok 分支也会返回空消息错误对象，真实 Err 也允许空消息。

## Nullable Flow 契约

`OpResult<T>` 的状态属性必须通过 nullable attributes 绑定分支载荷，并覆盖正向 guard 和反向 guard：

```csharp
[MemberNotNullWhen(true, nameof(Value))]
[MemberNotNullWhen(false, nameof(Error))]
public bool IsOk { get; }

[MemberNotNullWhen(true, nameof(Error))]
[MemberNotNullWhen(false, nameof(Value))]
public bool IsErr { get; }
```

目标使用体验：

```csharp
OpResult<User> result = GetUser();

if (result.IsOk)
{
    var userId = result.Value.Id;
}

if (!result.IsErr)
{
    var userName = result.Value.Name;
}

if (result.IsErr)
{
    Log(result.Error.Message);
}

if (!result.IsOk)
{
    Log(result.Error.Message);
}
```

未通过分支验证就解引用引用类型 `Value` / `Error` 时，Roslyn/LSP 应给出 nullable warning：

```csharp
OpResult<User> result = GetUser();

var userId = result.Value.Id;      // 应有 CS8602 等 nullable warning
var message = result.Error.Message; // 应有 CS8602 等 nullable warning
```

内置 Roslyn nullable analysis 只承诺引用类型风险提示。`OpResult<int>.Value + 1` 这类值类型误用在 v0.1.0 不承诺 warning；未来如需覆盖值类型误用，应通过自定义 analyzer 单独设计。

`OpResult` 没有成功载荷。它的 `IsErr == true` 和 `IsOk == false` 应保证 `Error` 可作为错误值使用：

```csharp
OpResult result = WriteToFile();

if (result.IsErr)
{
    Log(result.Error.Message);
}

if (!result.IsOk)
{
    Log(result.Error.Message);
}
```

## 默认值语义

`default(OpResult)` 和 `default(OpResult<T>)` 都定义为 Err 空消息：

```csharp
default(OpResult).IsErr == true;
default(OpResult).Error.Message == string.Empty;

default(OpResult<int>).IsErr == true;
default(OpResult<int>).Error.Message == string.Empty;
```

默认值不代表成功。实现必须保证默认实例的 `Error` getter 能提供非空空消息错误对象，避免破坏 `IsErr` / `Error` 的 nullable flow 契约。

## 构造方式

成功路径支持 non-generic Ok：

```csharp
OpResult WriteToFile()
{
    return OpResults.Ok();
}
```

有成功值的路径支持显式 Ok：

```csharp
OpResult<int> GetCount()
{
    return OpResults.Ok(42);
}
```

也支持直接返回成功值：

```csharp
OpResult<int> GetCount()
{
    return 42;
}
```

公开工厂标准如下：

```csharp
public static OpResult Ok();

public static OpResult<T> Ok<T>([DisallowNull] T? value)
    where T : notnull;

public static OpResult Err(string? message);

public static OpResult<T> Err<T>(string? message)
    where T : notnull;
```

`OpResults.Ok<T>(...)` 和 `T -> OpResult<T>` 成功隐式转换都按 non-null payload 设计。引用类型 null 调用应在 nullable-enabled 调用方产生 Roslyn/LSP warning；如果调用方通过 `null!`、nullable disabled、dynamic 或反射等方式绕过开发期提示，运行时必须在 Ok 构造边界抛出 `ArgumentNullException`，不创建 Ok(null)。

错误路径通过 `OpResults.Err(...)` / `OpResults.Err<T>(...)` 显式表达：

```csharp
OpResult WriteToFile()
{
    return OpResults.Err("write failed");
}

OpResult<User> GetUser()
{
    return OpResults.Err<User>("user not found");
}
```

`OpResults.Err(...)` 返回 `OpResult`，只用于 non-generic 结果。泛型结果必须使用 `OpResults.Err<T>(...)`。`OpResults.Err(...)` 不返回 `OpError`。

`OpError` 是错误详情类型，不是结果 carrier。v0.1.0 不提供 `OpError -> OpResult` 或 `OpError -> OpResult<T>` 隐式转换。

`OpResults.Err(null)` 和空白 message 在运行时归一化为 `string.Empty`，不抛异常。

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

`OpResult` 和 `OpResult<T>` 都是一等 Result 类型。组合 API 必须支持完整但克制的 void/value 矩阵：

- `OpResult` 可以继续到 `OpResult`。
- `OpResult` 可以继续到 `OpResult<T>`。
- `OpResult<T>` 可以继续到 `OpResult<TNext>`。
- `OpResult<T>` 可以继续到 `OpResult`。

`Then` 表示 Ok 后继续一个同步且可能失败的步骤。Err 分支短路，不调用后续步骤：

```csharp
public static OpResult Then(
    this OpResult result,
    Func<OpResult> onOk);

public static OpResult<T> Then<T>(
    this OpResult result,
    Func<OpResult<T>> onOk)
    where T : notnull;

public static OpResult<TNext> Then<T, TNext>(
    this OpResult<T> result,
    Func<T, OpResult<TNext>> onOk)
    where T : notnull
    where TNext : notnull;

public static OpResult Then<T>(
    this OpResult<T> result,
    Func<T, OpResult> onOk)
    where T : notnull;
```

`ThenAsync` 表示 Ok 后继续一个异步且可能失败的步骤。异步主线只使用 `Task`：

```csharp
public static Task<OpResult> ThenAsync(
    this OpResult result,
    Func<Task<OpResult>> onOk);

public static Task<OpResult<T>> ThenAsync<T>(
    this OpResult result,
    Func<Task<OpResult<T>>> onOk)
    where T : notnull;

public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
    this OpResult<T> result,
    Func<T, Task<OpResult<TNext>>> onOk)
    where T : notnull
    where TNext : notnull;

public static Task<OpResult> ThenAsync<T>(
    this OpResult<T> result,
    Func<T, Task<OpResult>> onOk)
    where T : notnull;

public static Task<OpResult> ThenAsync(
    this Task<OpResult> resultTask,
    Func<Task<OpResult>> onOk);

public static Task<OpResult<T>> ThenAsync<T>(
    this Task<OpResult> resultTask,
    Func<Task<OpResult<T>>> onOk)
    where T : notnull;

public static Task<OpResult<TNext>> ThenAsync<T, TNext>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task<OpResult<TNext>>> onOk)
    where T : notnull
    where TNext : notnull;

public static Task<OpResult> ThenAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task<OpResult>> onOk)
    where T : notnull;
```

`OnOk` / `OnOkAsync` 用于在 Ok 分支执行副作用，并保持原 Result 不变。`OnErr` / `OnErrAsync` 用于在 Err 分支执行副作用，并保持原 Result 不变。它们不负责恢复、转换或改变 Result 状态：

```csharp
public static OpResult OnOk(
    this OpResult result,
    Action onOk);

public static OpResult<T> OnOk<T>(
    this OpResult<T> result,
    Action<T> onOk)
    where T : notnull;

public static OpResult OnErr(
    this OpResult result,
    Action<OpError> onErr);

public static OpResult<T> OnErr<T>(
    this OpResult<T> result,
    Action<OpError> onErr)
    where T : notnull;
```

异步副作用只使用 `Task`：

```csharp
public static Task<OpResult> OnOkAsync(
    this OpResult result,
    Func<Task> onOk);

public static Task<OpResult<T>> OnOkAsync<T>(
    this OpResult<T> result,
    Func<T, Task> onOk)
    where T : notnull;

public static Task<OpResult> OnErrAsync(
    this OpResult result,
    Func<OpError, Task> onErr);

public static Task<OpResult<T>> OnErrAsync<T>(
    this OpResult<T> result,
    Func<OpError, Task> onErr)
    where T : notnull;

public static Task<OpResult> OnOkAsync(
    this Task<OpResult> resultTask,
    Func<Task> onOk);

public static Task<OpResult<T>> OnOkAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task> onOk)
    where T : notnull;

public static Task<OpResult> OnErrAsync(
    this Task<OpResult> resultTask,
    Func<OpError, Task> onErr);

public static Task<OpResult<T>> OnErrAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<OpError, Task> onErr)
    where T : notnull;
```

`Match` / `MatchAsync` 用于消费 Ok / Err 两个分支，支持返回值分支和副作用分支：

```csharp
public static TResult Match<TResult>(
    this OpResult result,
    Func<TResult> onOk,
    Func<OpError, TResult> onErr);

public static void Match(
    this OpResult result,
    Action onOk,
    Action<OpError> onErr);

public static TResult Match<T, TResult>(
    this OpResult<T> result,
    Func<T, TResult> onOk,
    Func<OpError, TResult> onErr)
    where T : notnull;

public static void Match<T>(
    this OpResult<T> result,
    Action<T> onOk,
    Action<OpError> onErr)
    where T : notnull;
```

`MatchAsync` 对应支持 `OpResult` / `OpResult<T>` receiver 与 `Task<OpResult>` / `Task<OpResult<T>>` receiver：

```csharp
public static Task<TResult> MatchAsync<TResult>(
    this OpResult result,
    Func<Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr);

public static Task MatchAsync(
    this OpResult result,
    Func<Task> onOk,
    Func<OpError, Task> onErr);

public static Task<TResult> MatchAsync<T, TResult>(
    this OpResult<T> result,
    Func<T, Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr)
    where T : notnull;

public static Task MatchAsync<T>(
    this OpResult<T> result,
    Func<T, Task> onOk,
    Func<OpError, Task> onErr)
    where T : notnull;

public static Task<TResult> MatchAsync<TResult>(
    this Task<OpResult> resultTask,
    Func<Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr);

public static Task MatchAsync(
    this Task<OpResult> resultTask,
    Func<Task> onOk,
    Func<OpError, Task> onErr);

public static Task<TResult> MatchAsync<T, TResult>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task<TResult>> onOk,
    Func<OpError, Task<TResult>> onErr)
    where T : notnull;

public static Task MatchAsync<T>(
    this Task<OpResult<T>> resultTask,
    Func<T, Task> onOk,
    Func<OpError, Task> onErr)
    where T : notnull;
```

`MapOk` / `MapErr`、显式 fallback API、`TryGetValue` / `TryGetError` 不作为 v0.1.0 核心 API。

## 异步模型

异步 Result 使用 .NET 原生形态：

```csharp
Task<OpResult>
Task<OpResult<T>>
```

扩展方法负责补齐链式组合体验，不引入 `AsyncOpResult`。

异步扩展方法以 `Task` 为标准，不引入 `ValueTask`。扩展方法本身不显式接收 `CancellationToken`；需要取消时，由调用方在业务方法参数或委托闭包中表达。

## 性能原则

`OpResult` / `OpResult<T>` 保持值类型容器，优先保护 Ok path。

`OpError` 使用 `sealed record class`，接受 Err path 上的小对象分配，以换取错误模型扩展时 Result 容器尺寸稳定。未来 `OpError` 增加 `Code`、`Cause`、`Metadata` 或错误链时，扩展成本应停留在 Err 对象上，而不是让每个 Ok result 都携带额外字段。

空消息错误可以作为内部单例复用，减少 Ok 分支 `Error` 兜底和空消息 Err 的重复分配。该复用是实现细节，不是 public API 语义；调用方不得依赖空错误对象的引用身份。

运行时策略优先避免 Result 访问器在分支不匹配时产生未被开发期诊断覆盖的异常。分支正确性由 `IsOk` / `IsErr`、nullable 标注、组合 API、测试和文档共同表达。非法 null 成功值在 Ok 构造边界抛出明确的 `ArgumentNullException`，避免坏状态进入 Ok path。

## 命名原则

API 命名优先降低业务开发者心智负担。

无成功值业务使用 `OpResult`；有成功值业务使用 `OpResult<T>`。不使用 `OpResult<Unit>` 或空对象模拟 void 成功值。

链式短路使用 `Then` / `ThenAsync` 表达业务步骤串联。

分支副作用使用 `OnOk` / `OnErr` 表达。

双分支消费统一使用 `Match` / `MatchAsync`。

失败结果创建使用 `OpResults.Err(...)` / `OpResults.Err<T>(...)`。泛型失败返回必须显式写出 `T`：

```csharp
return OpResults.Err<User>("user not found");
```

## 验收标准

实现必须包含 compile-only nullable flow 测试，并将关键 nullable warnings 作为 errors 检查。普通运行时单元测试不能替代 nullable flow 验收。

nullable flow 验收至少覆盖：

- 未 guard 的引用类型 `Value` 解引用应触发 nullable warning。
- 未 guard 的 `Error` 解引用应触发 nullable warning。
- `if (result.IsOk)` 后消费 `Value` 无 nullable warning。
- `if (!result.IsErr)` 后消费 `Value` 无 nullable warning。
- `if (result.IsErr)` 后消费 `Error` 无 nullable warning。
- `if (!result.IsOk)` 后消费 `Error` 无 nullable warning。
- `OpResult<User?>` / `OpResults.Ok<User?>(...)` 应触发 notnull / nullable 相关 warning。
- `OpResult<int>.Value` 未 guard 误用不属于 v0.1.0 内置 nullable warning 承诺。

运行时验收至少覆盖：

- `default(OpResult)` 是 Err 空消息。
- `default(OpResult<T>)` 是 Err 空消息。
- Err 分支读取 `Value` 返回 `default(T)`，不抛异常。
- Ok 分支读取 `Error` 返回空消息 `OpError`，不抛异常。
- `OpResults.Ok<T>(null!)` 和 `T -> OpResult<T>` 隐式转换中的 null 值抛 `ArgumentNullException`。
- `OpResults.Err(null)`、空字符串和空白字符串归一为 `string.Empty`。
- `OpResults.Err(...)` 返回 `OpResult`。
- `OpResults.Err<T>(...)` 返回 `OpResult<T>`。
- `OpError` 不能隐式转换为 `OpResult` 或 `OpResult<T>`。

文档验收至少覆盖：

- README 必须新增 non-generic `OpResult` 示例。
- README 中泛型失败返回示例必须使用 `OpResults.Err<T>(...)`。
- README 不得再写 `OpResults.Err(...)` 返回 `OpError`。
- README 必须说明 `Value` / `Error` 的错分支 fallback 是运行时兜底，不是推荐消费路径。
- README 必须说明正确分支判断方式是 `IsOk` / `IsErr` / `Then` / `Match`，不是属性空值或错误消息内容。
