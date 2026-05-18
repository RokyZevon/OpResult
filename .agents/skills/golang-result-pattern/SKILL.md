---
name: golang-result-pattern
description: Go (T, error) 错误处理模式的设计参考。当需要理解 Go 的错误哲学、error 接口、wrapping 协议、comma-ok 惯例或对比 Rust Result 时使用此 skill。
---

# Go 错误处理设计参考

## 设计哲学

- **错误即值**：error 非控制流机制，是普通值——可存储、传递、编程。Rob Pike 2015 年的核心洞见。
- **显式优于隐式**：所有可失败函数显式返回 error。调用者内联处理，无隐藏跳转。
- **错误非异常**：文件打开失败、网络超时是正常操作条件，不值得 panic。仅编程 bug 才 panic。
- **接口驱动**：整个错误系统建立在单个最小接口上，无栈追踪、无错误码、无严重级别——全留给用户自行实现。

## 核心机制

### `error` 接口

```go
type error interface {
    Error() string
}
```

内置于 universe block，结构化类型——任何拥有 `Error() string` 方法的类型自动满足。刻意极简，最大灵活性。

### 构造

| 方式 | 签名 | 说明 |
|------|------|------|
| `errors.New` | `func New(text string) error` | 最简工厂，返回未导出的 `*errorString` |
| `fmt.Errorf` | `func Errorf(format string, a ...any) error` | 格式化构造；`%w` 动词创建包装错误（1.13+） |

## `(T, error)` 返回惯例

Go 的 Result Pattern 等价物。非类型系统强制，而是全民约定。

```go
func ReadFile(name string) ([]byte, error) { ... }

data, err := ReadFile("config.json")
if err != nil {
    return nil, fmt.Errorf("load config: %w", err)
}
// use data
```

### 与 Rust `Result<T, E>` 的关键差异

| 维度 | Rust `Result<T, E>` | Go `(T, error)` |
|------|---------------------|------------------|
| 类型安全 | 编译器强制的 sum type | 约定，非强制 |
| 忽略错误 | 须显式 `let _ =`；`#[must_use]` 警告 | `_, _ =` 或直接不检查；依赖 linter |
| 零值问题 | Err 变体中不存在 T 值 | T 和 error 同时存在，T 为零值 |
| 链式组合 | `.map()`, `.and_then()`, `?` | 手动 `if err != nil` |
| 穷尽匹配 | `match` 编译器保证 | 无对应机制 |
| 内存模型 | 栈分配 tagged union，零开销 | error 接口装箱，堆分配 |

### 两值共存问题

`(T, error)` 中 T 和 error 同时存在。T 是否有意义完全取决于 API 契约：

**T 有意义**——流式/增量 API。必须先处理 T 再检查 error：

```go
n, err := reader.Read(buf)
process(buf[:n]) // n 可能 > 0，即使 err == io.EOF
if err != nil { ... }
```

**T 应忽略**——构造/分配 API。error 非 nil 时 T 通常为 nil/零值：

```go
f, err := os.Open("x.txt") // err != nil 时 f 为 nil
```

语言无法强制此规则。文档是唯一保证。

## 哨兵错误与自定义类型

### 哨兵错误

包级预声明的 error 值，用于比较。命名约定：`Err<描述>`。

```go
var ErrNotFound = errors.New("not found")
// 标准库：io.EOF, sql.ErrNoRows, os.ErrNotExist, context.Canceled
```

哨兵错误一旦导出即为公开 API，不可随意变更。使用 `errors.Is` 而非 `==` 比较（wrapping 感知）。

### 自定义错误类型

struct 实现 `Error() string`，携带结构化上下文：

```go
type PathError struct {
    Op   string
    Path string
    Err  error
}
func (e *PathError) Error() string { return e.Op + " " + e.Path + ": " + e.Err.Error() }
func (e *PathError) Unwrap() error { return e.Err }
```

### 行为接口

定义超越 `Error()` 的方法，按行为断言而非类型——降低包间耦合：

```go
// net.Error
type Error interface {
    error
    Timeout() bool
}
```

### nil 接口陷阱

typed nil 指针赋给 `error` 接口 → 接口非 nil（接口持有 `(T=*MyError, V=nil)`）：

```go
func bad() error {
    var p *MyError = nil
    return p // BUG: 返回非 nil error！
}
// 正确做法：return nil
```

## Wrapping 协议与错误链

### 包装

```go
return fmt.Errorf("opening config: %w", err)      // 单包装（1.13+）
return fmt.Errorf("problems: %w and %w", e1, e2)  // 多包装（1.20+）
```

`%w` 保留错误链；`%v` 转为字符串，断链。**混用 `%v` 是常见错误。**

### Unwrap 协议

| 签名 | 版本 | 语义 |
|------|------|------|
| `Unwrap() error` | 1.13 | 线性链，单父错误 |
| `Unwrap() []error` | 1.20 | 错误树，多父错误 |

`errors.Unwrap()` 函数仅调用 `Unwrap() error`，忽略切片形式。`errors.Is` 和 `errors.As` 识别两种形式，对树做深度优先遍历。

### `errors.Is`

```go
func Is(err, target error) bool
```

链/树感知的值匹配。自定义覆盖：实现 `Is(error) bool` 方法（仅浅比较，不递归）。

```go
// syscall.Errno 的 Is 实现——多个 errno 映射到同一哨兵
func (e Errno) Is(target error) bool {
    switch target {
    case oserror.ErrPermission: return e == EACCES || e == EPERM
    case oserror.ErrNotExist:   return e == ENOENT
    }
    return false
}
```

### `errors.As`

```go
func As(err error, target any) bool
```

链/树感知的类型匹配。`target` 须为指向 error 或接口类型的非 nil 指针。自定义覆盖：实现 `As(any) bool` 方法，负责设置 target。

```go
var pathErr *os.PathError
if errors.As(err, &pathErr) {
    fmt.Println("failed path:", pathErr.Path)
}
```

### `errors.Join`（1.20）

```go
func Join(errs ...error) error
```

聚合多错误，nil 被过滤。返回的错误实现 `Unwrap() []error`。

## Comma-Ok 惯例

Go 的另一种"安全取值"模式。语言级仅三种，无其他：

| 场景 | 语法 | ok 含义 |
|------|------|---------|
| Map 查找 | `v, ok := m[key]` | key 存在 |
| 类型断言 | `v, ok := x.(T)` | x 持有类型 T（无 ok 则失败 panic） |
| Channel 接收 | `v, ok := <-ch` | 值由 send 产生（false = channel 已关闭且空） |

## `panic`/`recover`

### 定位

- **panic**：编程 bug——越界、nil 解引用、不可能状态。类比 Rust 的 `panic!`。
- **recover**：仅在 `defer` 函数内有效，捕获 panic 值，程序继续执行。

### 边界规则

panic 不应跨包边界传播。库内部可用 panic+recover 做优化（如 `encoding/json` 深递归），但必须在 API 边界 recover 为 error。

### `Must` 模式

仅用于包级 `var`/`init` 中编译期可检测的程序员错误：

```go
var tmpl = template.Must(template.New("page").Parse(htmlStr))
var re   = regexp.MustCompile(`\d+`)
```

## 标准库惯用法

### "错误即值"累积器

struct 内部存 err，连续操作短路，最后一次检查。`bufio.Writer`、`archive/zip.Writer` 皆用此模式：

```go
type errWriter struct {
    w   io.Writer
    err error
}
func (ew *errWriter) write(buf []byte) {
    if ew.err != nil { return }
    _, ew.err = ew.w.Write(buf)
}
// 连续 write，最后检查 ew.err
```

### defer 注解模式

命名返回值 + defer 闭包统一添加上下文：

```go
func doWork() (err error) {
    defer func() {
        if err != nil { err = fmt.Errorf("doWork: %w", err) }
    }()
    // ... 多个可能返回 err 的操作
}
```

优点：DRY，不遗漏新返回路径。缺点：可读性下降，需命名返回值，有过度包装风险。

### 上下文逐层添加

```go
return fmt.Errorf("load config: %w", err)
// 产生："load config: parse config.json: invalid character '}'"
```

仅在**抽象边界**处添加上下文，非每个 `if err != nil` 都包装。

### 反模式

| 反模式 | 说明 |
|--------|------|
| 库中 `log.Fatal` | 库应返回 error，决不终止进程 |
| 吞没错误 | `_, _ = Fn()` 且无注释说明理由 |
| 裸 `return err` | 不加上下文，调用者得到无定位信息的消息 |
| `err.Error() == "..."` | 字符串比较脆弱；应用 `errors.Is` / `errors.As` |
| `%v` 替代 `%w` | 断链，`errors.Is`/`As` 失效 |
| 返回 typed nil | nil 接口陷阱，见上文 |
| panic 作控制流 | panic 仅用于不可恢复的编程 bug |

## 关键设计决策

| 决策 | 理由 |
|------|------|
| 无 sum type，用 `(T, error)` 元组 | Go 团队认为 variant type 与 interface 重叠且混淆 |
| E 固定为 `error` 接口 | 最大灵活性；代价是接口装箱堆分配 |
| 无 `?` 操作符 / `try` 内置函数 | 2019 提案被社区否决（824 反对 vs 353 赞成）；隐藏控制流，不鼓励上下文添加 |
| 无编译期强制错误检查 | 依赖 linter（`errcheck`、`golangci-lint`）填补 |
| `error(nil)` 表示成功 | 无独立 Ok 状态；零值 nil 即成功——与 Rust 的 `default` 即 Err 相反 |
| 1.13 引入 wrapping | 晚于语言初始设计；社区 `pkg/errors` 先行验证了需求 |
| 1.20 引入 `errors.Join` + 多 `%w` | 多错误聚合标准化，替代 `hashicorp/go-multierror` |
| 无栈追踪 | 刻意选择——保持 error 轻量；需要时自行实现或用第三方库 |