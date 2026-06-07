# OpError InnerError Design

## 背景

当前 `OpError` 只有一段 `Message`，足够表达单层失败，但无法在上层返回错误时保留下层失败来源。例如 `GetProfile` 调用 `GetUser` 失败时，只能返回新的 `"get profile failed"`，或者手动把下层消息拼进字符串。这样会丢失结构，也不利于一行日志记录。

本次设计的目标是在保持库精简、高性能、AOT 友好和调用点人体工学的前提下，为 `OpError` 增加直接内层错误引用，并提供一个明确的上层包装 API。

## 目标

- `OpError` 可以结构化保存直接下层错误：`InnerError`。
- 调用点可以自然写出：

  ```csharp
  var getUserResult = GetUser(1);
  if (getUserResult.IsErr)
      return getUserResult.Error.ToErr("get user failed");
  ```

- `ToString()` 支持一行输出完整错误链，便于直接日志记录。
- `Then` / `ThenAsync` 短路时保留原始 `OpError` 对象，不重建、不丢链。
- `TryInvoke` / `TryInvokeAsync` 把非取消异常映射为轻量 `OpError` 链，但不把 `Exception` 对象保存进 `OpError`。
- 继续避免反射、源生成器、动态代码、重 metadata 和大对象图，保持 AOT 兼容与低开销。

## 非目标

- 本次设计不实现 `result.ToErr("...")`。
- 本次设计不实现 analyzer 或 source generator。
- 本次设计不公开 `FromException`。
- 本次设计不在 `OpError` 中保存 `Exception` 对象、异常堆栈、异常类型字段、错误码、字典 metadata 或其他结构化扩展数据。
- 本次设计不新增 `OpResults.Err<T>(string?, OpError?)`，避免泛型 factory 面继续膨胀。
- `OpError.ToString()` 是人类可读显示，不是可解析协议；调用方不应依赖它做机器解析。

## 公开 API

### OpError

```csharp
public sealed record class OpError
{
    public string Message { get; }
    public OpError? InnerError { get; }

    public override string ToString();
}
```

`InnerError` 表示直接内层错误，语义接近 BCL `Exception.InnerException` 和 Rust `Error::source()` 的“错误链下一层来源”。这里使用 `InnerError` 命名，降低不熟悉 Rust 的 .NET 用户的心智负担。

### 工厂方法

```csharp
public static OpError Err(string? message, OpError? innerError);
```

行为：

- `innerError is null` 等价于只创建单层错误。
- `message` 为 `null`、空字符串或空白字符串时，仍允许构造空消息错误节点。
- `OpResults.Err(string?)` 保持已有入口。
- 不新增 `OpResults.Err<T>(string?, OpError?)`。

### 扩展方法

```csharp
public static OpError ToErr(this OpError innerError, string? message);
```

行为：

- 返回一个新的外层 `OpError`，其 `InnerError` 是接收者 `innerError`。
- `innerError is null` 时抛出 `ArgumentNullException`，参数名为 `innerError`。
- 推荐消费形态：

  ```csharp
  var getUserResult = GetUser(1);
  if (getUserResult.IsErr)
      return getUserResult.Error.ToErr("get user failed");
  ```

这个形态依赖现有 nullable flow：`IsErr` 守卫后 `result.Error` 为非空；未守卫或在 `IsOk` 分支内调用 `result.Error.ToErr(...)` 会得到可空性警告。

## ToString 规则

`OpError.ToString()` 输出从外层到内层的一行错误链。

规则：

- 从当前错误开始，沿 `InnerError` 向内遍历。
- 跳过 `Message` 为 `null`、空字符串或空白字符串的节点。
- 非空消息之间使用 `" -> "` 连接。
- 如果整条链没有任何非空消息，返回 `"<error>"`。
- 不做循环检测。`OpError` 的公开 API 不提供可变 `InnerError`，正常构造不会产生环。

示例：

```csharp
var inner = OpResults.Err("user not found");
var outer = inner.ToErr("get user failed");

outer.ToString();
// get user failed -> user not found
```

空消息节点保留结构，但不污染显示：

```csharp
var inner = OpResults.Err("user not found");
var outer = inner.ToErr("");

outer.InnerError == inner; // true
outer.ToString();          // user not found
```

全空消息链使用占位符：

```csharp
OpResults.Err("").ToString();
// <error>
```

## Then / ThenAsync 短路

所有 `Then` / `ThenAsync` 在输入结果已经是错误时，必须保留同一个 `OpError` 引用。

错误行为：

```csharp
return OpResults.Err(result.Error.Message);
```

目标行为：

```csharp
return OpResult.Err(result.Error);
```

或等价内部路径。关键要求是：短路不得通过 `Message` 重建错误，否则会丢失 `InnerError` 链，也会破坏引用保留。

## TryInvoke / TryInvokeAsync 异常映射

`TryInvoke` / `TryInvokeAsync` 保持当前四个公开 overload，不新增公开 adapter API。

非取消异常映射规则：

- `OperationCanceledException`、`TaskCanceledException` 及派生类型继续向外传播，不转换为 `Err`。
- 其他异常转换为 `OpError`。
- 如果异常有 `InnerException`，递归转换为 `InnerError` 链。
- 每一层异常的 `Message` 遵循 BCL `Exception.ToString()` 的片段风格：
  - 使用 `exception.GetType().ToString()` 作为类型名。
  - 如果 `exception.Message` 非空：`"Full.Type.Name: " + exception.Message`。
  - 如果 `exception.Message` 为空：`"Full.Type.Name"`。
- 不保存原始 `Exception` 对象、堆栈、结构化异常类型字段或 metadata。

示例：

```csharp
try
{
    throw new InvalidOperationException(
        "outer failed",
        new ArgumentException("bad user id"));
}
catch (Exception exception)
{
    var error = /* internal exception mapper */ exception;
}
```

映射后的显示：

```text
System.InvalidOperationException: outer failed -> System.ArgumentException: bad user id
```

空异常消息只保留类型名：

```text
System.InvalidOperationException
```

`Operation returned null.` 仍保持当前固定消息，不加异常类型前缀，也不创建内层错误。

## 文档要求

英文与中文 README 都需要同步：

- 展示 `InnerError` 的结构化包装。
- 推荐 `result.Error.ToErr("...")` 消费形态。
- 展示 `ToString()` 一行日志输出。
- 展示 `TryInvoke` 异常链会包含异常类型名和内层异常消息。
- 明确 `ToString()` 是显示用途，不是稳定解析协议。

## 测试要求

需要覆盖：

- `OpError.InnerError` 保存直接内层错误。
- `OpResults.Err(string?, OpError?)` 的 `null` inner 行为。
- `OpError.ToString()` 的正常链、空消息节点跳过、全空链占位符。
- `OpError.ToErr(string?)` 的包装行为和 `null` receiver guard。
- `result.Error.ToErr(...)` 的 nullable flow：`IsErr` 分支通过，未守卫或 `IsOk` 分支警告。
- `Then` / `ThenAsync` 短路保留同一 `OpError` 引用。
- `TryInvoke` / `TryInvokeAsync` 异常映射包含异常类型名、内层异常链、空异常消息类型名。
- cancellation 仍传播。
- null task / null payload failure 仍返回 `"Operation returned null."`。
