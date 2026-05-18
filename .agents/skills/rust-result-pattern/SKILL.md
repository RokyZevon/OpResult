---
name: rust-result-pattern
description: Rust Result<T, E> 的 API 设计参考。当需要理解 Result Pattern 的设计哲学、API 分类、错误传播机制或惯用法时使用此 skill。
---

# Rust `Result<T, E>` 设计参考

## 设计哲学

- **零异常**：Rust 无异常机制。`Result<T, E>` 处理可恢复错误，`panic!` 处理不可恢复错误（bug/违反不变量）。二者完全隔离——Result 不捕获 panic，panic 不是 Err。
- **显式错误流**：所有可失败操作返回 `Result`。调用者被强制处理错误，无隐式控制流。`#[must_use]` 标注确保编译器警告未处理的 Result。
- **E 无约束**：`E` 无任何 trait bound。可以是 `String`、`i32`、`()`、自定义类型。仅在特定方法上按需添加 bound（如 `unwrap` 要求 `E: Debug`）。
- **零开销**：`Result` 是栈分配的 tagged union（`enum`），无堆分配、无栈展开、无栈追踪捕获。

## 核心定义

```rust
pub enum Result<T, E> {
    Ok(T),
    Err(E),
}
```

两个变体，穷尽匹配。无"空"/"默认"状态——构造时必须选择 Ok 或 Err。

## API 分类

### 查询

| 方法 | 签名 | 说明 |
|------|------|------|
| `is_ok` | `fn is_ok(&self) -> bool` | |
| `is_err` | `fn is_err(&self) -> bool` | |
| `is_ok_and` | `fn is_ok_and(self, f: FnOnce(T) -> bool) -> bool` | Ok 且谓词为真 |
| `is_err_and` | `fn is_err_and(self, f: FnOnce(E) -> bool) -> bool` | Err 且谓词为真 |

### 变换

**变换 Ok 值（Err 透传）：**

| 方法 | 签名 | 说明 |
|------|------|------|
| `map` | `fn map<U>(self, f: FnOnce(T) -> U) -> Result<U, E>` | functor map |
| `inspect` | `fn inspect(self, f: FnOnce(&T)) -> Result<T, E>` | 副作用观察，不改变值 |

**变换 Err 值（Ok 透传）：**

| 方法 | 签名 | 说明 |
|------|------|------|
| `map_err` | `fn map_err<F>(self, f: FnOnce(E) -> F) -> Result<T, F>` | |
| `inspect_err` | `fn inspect_err(self, f: FnOnce(&E)) -> Result<T, E>` | |

**穷举变换（折叠为其他类型）：**

| 方法 | 签名 | 说明 |
|------|------|------|
| `map_or` | `fn map_or<U>(self, default: U, f: FnOnce(T) -> U) -> U` | Ok 走 f，Err 走 default（急切求值） |
| `map_or_else` | `fn map_or_else<U>(self, d: FnOnce(E) -> U, f: FnOnce(T) -> U) -> U` | 等价于穷举 match |

> `map_or_else` 本质上就是 match 的函数式写法。

### 链式组合（Monadic）

| 方法 | 签名 | 说明 |
|------|------|------|
| `and_then` | `fn and_then<U>(self, f: FnOnce(T) -> Result<U, E>) -> Result<U, E>` | flatMap/bind。链接可失败操作 |
| `or_else` | `fn or_else<F>(self, f: FnOnce(E) -> Result<T, F>) -> Result<T, F>` | 错误恢复。Err 时尝试备选操作 |

```rust
// and_then 链式：每一步都可能失败
parse_input(raw)
    .and_then(validate)
    .and_then(save_to_db)
```

### 布尔组合子

将 Ok 视为 true，Err 视为 false：

| 方法 | 签名 | 说明 |
|------|------|------|
| `and` | `fn and<U>(self, res: Result<U, E>) -> Result<U, E>` | self 为 Ok 时返回 res，否则返回 self 的 Err |
| `or` | `fn or<F>(self, res: Result<T, F>) -> Result<T, F>` | self 为 Ok 时返回 self，否则返回 res |

### 值提取

**安全提取（不 panic）：**

| 方法 | 签名 | 说明 |
|------|------|------|
| `unwrap_or` | `fn unwrap_or(self, default: T) -> T` | 急切默认值 |
| `unwrap_or_else` | `fn unwrap_or_else(self, f: FnOnce(E) -> T) -> T` | 惰性默认值 |
| `unwrap_or_default` | `fn unwrap_or_default(self) -> T` where `T: Default` | 使用 T 的默认值 |
| `ok` | `fn ok(self) -> Option<T>` | 丢弃错误 |
| `err` | `fn err(self) -> Option<E>` | 丢弃成功值 |

**Panic 提取（仅用于测试/已知安全场景）：**

| 方法 | 签名 | 说明 |
|------|------|------|
| `unwrap` | `fn unwrap(self) -> T` where `E: Debug` | Err 时 panic |
| `expect` | `fn expect(self, msg: &str) -> T` where `E: Debug` | Err 时 panic，附自定义消息 |
| `unwrap_err` | `fn unwrap_err(self) -> E` where `T: Debug` | Ok 时 panic |
| `expect_err` | `fn expect_err(self, msg: &str) -> E` where `T: Debug` | Ok 时 panic |

> `expect` 优于 `unwrap`——消息应解释为什么 Ok 是预期的，而非描述错误。

### 类型转换

| 方法 | 签名 | 说明 |
|------|------|------|
| `transpose` | `Result<Option<T>, E> → Option<Result<T, E>>` | 交换嵌套层 |
| `flatten` | `Result<Result<T, E>, E> → Result<T, E>` | 去除一层嵌套 |

### 迭代

Result 可视为 0 或 1 个元素的容器：

| 方法 | 说明 |
|------|------|
| `iter` / `iter_mut` | 引用迭代 |
| `into_iter` | 消费迭代 |

## `?` 操作符与错误传播

`?` 是 Rust 对 Result 的语法糖，脱糖为：

```rust
// val? 等价于：
match val {
    Ok(v) => v,
    Err(e) => return Err(From::from(e)),
}
```

关键机制：
- Err 时自动调用 `From::from(e)` 转换错误类型，然后提前返回
- 要求所在函数返回 `Result`（或实现 `FromResidual` 的类型）
- 不同错误类型可通过 `impl From<SourceErr> for TargetErr` 统一

```rust
fn process() -> Result<Data, AppError> {
    let raw = read_file("input.txt")?;   // io::Error → AppError via From
    let parsed = parse(raw)?;             // ParseError → AppError via From
    Ok(parsed)
}
```

`?` 使可失败操作链具有线性、命令式的阅读体验，同时保持类型安全的错误传播。

## 惯用法

### 模式匹配（最基本）

```rust
match result {
    Ok(val) => { /* 使用 val */ },
    Err(err) => { /* 处理 err */ },
}
```

穷尽匹配，编译器保证所有分支被覆盖。

### 类型别名简化签名

```rust
type Result<T> = std::result::Result<T, io::Error>;
// 之后：fn read() -> Result<String>  等价于 Result<String, io::Error>
```

标准库中 `io::Result<T>` 即此模式。

### 常见错误类型模式

| 模式 | 适用场景 |
|------|----------|
| 具体类型 `Result<T, io::Error>` | 单一错误源 |
| 自定义枚举 `Result<T, MyError>` | 需穷举匹配错误变体 |
| `Box<dyn Error>` | 应用层，多种错误混合 |

## Trait 实现

### 相等与排序

- `PartialEq` / `Eq`：同变体比较内部值；`Ok ≠ Err` 恒成立
- `PartialOrd` / `Ord`：**Ok < Err**（跨变体时）；同变体内比较内部值
- 条件实现：仅当 T、E 满足对应 trait 时可用

### 复制与调试

- `Clone`：当 `T: Clone, E: Clone`
- `Copy`：当 `T: Copy, E: Copy`
- `Debug`：格式化为 `Ok(...)` 或 `Err(...)`
- `Hash`：当 `T: Hash, E: Hash`

### FromIterator（收集语义）

```rust
impl<A, E, V: FromIterator<A>> FromIterator<Result<A, E>> for Result<V, E>
```

将 `Iterator<Item = Result<A, E>>` 收集为 `Result<Vec<A>, E>`。**遇到第一个 Err 即短路**。

### IntoIterator

三种实现（消费/不可变引用/可变引用），每种产出 0 或 1 个元素。

## 关键设计决策总结

| 决策 | 理由 |
|------|------|
| E 无约束 | 最大泛化，适配任何场景 |
| unwrap 会 panic | 逃生舱，不鼓励生产使用 |
| 无裸 value 属性 | 强制显式处理两种状态 |
| Ok < Err（排序） | 将成功视为"正常"，错误视为"异常"的自然序 |
| FromIterator 短路 | 与 and_then 链的语义一致：首个错误即终止 |
| ? 自动 From 转换 | 统一不同错误类型而不丧失类型安全 |