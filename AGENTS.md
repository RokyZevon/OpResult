# OpResult

Rust 风格的 Result Pattern，用于 .NET。零异常，OpResult 自身零堆分配。

## 构建与测试

```sh
dotnet build
dotnet test
```

## 目标框架

- **TFM**: `net6.0`
- **LangVersion**: `latest`
- **Nullable**: `enable`
- **ImplicitUsings**: `enable`

## 核心类型

所有核心类型必须是 `readonly record struct`。禁止使用 `class` 或 `record class`。

### `OpResult<T, E>`

- 主要的 Result 类型。两个泛型参数：`T`（成功值），`E`（错误值）。
- **E 无任何约束** —— 可以是 `string`、`int`、自定义类型，任何类型皆可。对标 Rust 的 `Result<T, E>`。
- 内部字段：`bool _isOk`、`T _value`、`E _error`。
- `default` 实例视为 Err（`_isOk` 默认值为 `false`）。

### `OpResult<T>`

- 便捷类型。内部组合 `OpResult<T, OpError>`（委托，非继承）。
- 暴露与 `OpResult<T, E>` 完全相同的 API 表面。
- 工厂方法：`Ok(T value)`、`Err(OpError error)`。

### `OpError`

- `readonly record struct OpError(string Code, string Message)`。
- 内置轻量错误类型。实现 `IOpError`。
- 工厂：`OpError.Create(string code, string message)`、`OpError.Create(string message)`（Code 默认为 `""`）。

### `IOpError`

- `interface IOpError { string Code { get; } string Message { get; } }`
- 可选协议。不参与任何泛型约束。
- 仅用于下游消费（例如 `if (error is IOpError e) log(e.Message)`）。

### `OpResult`（静态类）

- 非泛型静态辅助类，提供无需指定类型参数的工厂方法。
- `OpResult.Ok<T>(T value)` → `OpResult<T>`
- `OpResult.Ok<T, E>(T value)` → `OpResult<T, E>`
- `OpResult.Err<T>(string message)` → `OpResult<T>`
- `OpResult.Err<T, E>(E error)` → `OpResult<T, E>`

## API 表面

仅允许以下公开成员存在于 `OpResult<T, E>` 上。未经明确批准，禁止添加其他成员。

### 属性

| 成员 | 类型 | 描述 |
|------|------|------|
| `IsOk` | `bool` | Ok 状态时为 `true` |
| `IsErr` | `bool` | Err 状态时为 `true`（包括 `default` 实例） |

### 工厂方法与转换

| 成员 | 签名 |
|------|------|
| `Ok` | `static OpResult<T, E> Ok(T value)` |
| `Err` | `static OpResult<T, E> Err(E error)` |
| 隐式转换 | `T` → `OpResult<T, E>`（Ok） |
| 隐式转换 | `E` → `OpResult<T, E>`（Err） |

> **已知限制**：当 `T == E` 时，隐式转换存在歧义。这是设计如此；此情况下用户必须使用显式的 `Ok()` / `Err()` 工厂方法。

### 核心方法

| 方法 | 签名 | 描述 |
|------|------|------|
| `Match` | `TOut Match<TOut>(Func<T, TOut> onOk, Func<E, TOut> onErr)` | 穷举匹配。`default` 实例走 `onErr`。 |
| `Match` | `void Match(Action<T> onOk, Action<E> onErr)` | void 重载。 |
| `Map` | `OpResult<U, E> Map<U>(Func<T, U> map)` | 变换 Ok 值。Err 时透传。 |
| `MapErr` | `OpResult<T, F> MapErr<F>(Func<E, F> map)` | 变换 Err 值。Ok 时透传。 |
| `AndThen` | `OpResult<U, E> AndThen<U>(Func<T, OpResult<U, E>> bind)` | 链式 fallible 操作（flatMap/bind）。 |
| `TryGetValue` | `bool TryGetValue(out T value)` | Ok 时返回 `true` + 值；Err 时返回 `false` + `default`。 |
| `TryGetError` | `bool TryGetError(out E error)` | Err 时返回 `true` + 错误；Ok 时返回 `false` + `default`。 |

### 委托安全

- 所有委托参数不可为 null，应使用 `System.Diagnostics.CodeAnalysis` 特性（如 `[DisallowNull]`）在编译期暴露 null 误用。
- 如果委托在运行时为 `null`，方法不得抛出异常；视为 Err，使用 `default(E)`（或 `Match<TOut>` 中使用 `default(TOut)`），不执行任何用户回调。
- 如果用户委托抛出异常，**不捕获，让异常自然传播**。零异常保证的边界是 OpResult 自身的代码，不延伸到用户提供的委托。这与 Rust 的行为一致（panic 不被 Result 捕获）。

## 设计红线

以下规则是绝对的。任何情况下都禁止违反。

1. **禁止异常** —— OpResult 自身的代码禁止抛出任何异常。不在构造函数中，不在属性中，不在方法中，不在运算符中。用户委托的异常不在此保证范围内，自然传播。
2. **禁止裸 `Value`/`Error` 属性** —— 所有值访问必须通过 `Match`、`TryGetValue` 或 `TryGetError`。永远不暴露直接返回 `T` 或 `E` 的属性。
3. **禁止约束 `E`** —— `OpResult<T, E>` 必须保持无约束。不添加 `where E : IOpError` 或任何其他约束。
4. **OpResult 自身禁止堆分配** —— 类型是 `readonly record struct`。不装箱，不分配。
5. **`default` 即 Err** —— `default(OpResult<T, E>)` 必须表现为 Err，错误值为 `default(E)`。永远不将其特殊处理为 Ok。

## 文件结构

| 文件 | 内容 |
|------|------|
| `OpResult.csproj` | 项目文件，`net6.0` 目标 |
| `IOpError.cs` | `IOpError` 接口 |
| `OpError.cs` | `OpError` readonly record struct |
| `OpResult{T,E}.cs` | `OpResult<T, E>` 核心类型 |
| `OpResult{T}.cs` | `OpResult<T>` 便捷类型 |
| `OpResult.cs` | `OpResult` 静态工厂类 |
