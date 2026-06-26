# OpResult 单包 Analyzer 分发设计

## 背景

OpResult 是一个实验性 .NET Result Pattern 库，核心语义目标是显式表达 `Ok` / `Err` 业务流。已有设计结论保持不变：

- `OpResult` / `OpResult<T>` 是轻量 value-type result carrier。
- `OpError` 是 immutable reference-type error node，用于冷路径错误详情和 `InnerError` 链。
- 成功路径应保持低成本；失败路径才携带 `OpError` 详情。
- C# / .NET 6 到 .NET 10 不能完整表达 `Result<T>` 的 union 语义。
- Nullable flow attributes 能覆盖部分 reference-type payload 场景，但不能完整覆盖 value-type payload、unused result、伪分支判断和错误链丢失。

因此，Analyzer 是 OpResult 语义模型的一部分，不是额外风格检查。

本设计废弃旧的三包拆分方案。新的分发决策以现代 .NET / NuGet 官方实践为基线：默认安装的库包同时分发 runtime assembly 和 Roslyn analyzer asset。

## 问题

OpResult 需要通过 analyzer 保护运行时类型系统无法表达的语义：

- 不应在没有证明结果为 `Ok` 时读取 `Value`。
- 不应在没有证明结果为 `Err` 时读取 `Error`。
- 不应使用 `Value == null`、`Value != null`、`Error == null`、`Error != null`、null pattern 或 `Error.Message` 空/非空消息测试判断结果分支。
- 不应无意丢弃返回的 `OpResult` / `OpResult<T>`。
- 上层短路失败时，不应只从 `result.Error.Message` 重建错误而丢失原始 `OpError.InnerError` 链。

同时，分发设计必须保持 `dotnet add package RokyZevon.OpResult` 这一默认体验，不引入 runtime-only 包、analyzer-only 包或 meta package。

## 设计决策

使用单 NuGet 包分发：

```text
RokyZevon.OpResult
```

默认用户安装：

```bash
dotnet add package RokyZevon.OpResult
```

该包同时包含：

- runtime assembly：`lib/net6.0/...`、`lib/net8.0/...`、`lib/net10.0/...`
- analyzer assembly：`analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll`

Analyzer 实现应位于独立 assembly / project 中，但该 analyzer project 不作为公开独立 NuGet 包发布。它只作为 `RokyZevon.OpResult` 包内的 analyzer asset 被打包。

不提供 runtime-only 包。用户如果需要调整 analyzer 行为，应使用 `.editorconfig`、`NoWarn` 或等价的标准 .NET diagnostic 配置，而不是选择另一个包。

## 事实依据

- Roslyn SDK 文档支持通过 NuGet 包分发 analyzers，使诊断参与 IDE 和命令行构建。
- NuGet analyzer conventions 使用 `analyzers/dotnet/cs` 作为 C# analyzer asset 路径。
- NuGet 官方打包文档要求发布前验证包安装和文件落位。
- 三包拆分不是 Microsoft Learn 明确推荐的库 + analyzer 分发范式；单包默认带 analyzer 更贴近官方文档描述的默认体验。

## 目标

- 保持 `RokyZevon.OpResult` 是唯一正常安装入口。
- 默认安装即包含 runtime API 和 analyzer diagnostics。
- Analyzer diagnostics 在 IDE 和 `dotnet build` / CI 中都生效。
- Analyzer assembly 与 runtime assembly 物理隔离，避免把 Roslyn 依赖带入 runtime 资产。
- Analyzer 规则优先报告明确语义错误，不做风格强制。
- 第一版只提供 diagnostics，不提供 code fix。
- 默认 severity 为 `warning`，允许消费者通过 `.editorconfig` 提升、降低或关闭。

## 非目标

- 不引入 `RokyZevon.OpResult.Core`。
- 不引入 `RokyZevon.OpResult.Analyzers` 公开包。
- 不引入 meta package。
- 不提供 runtime-only 安装路径。
- 不重设计 `OpResult<T>`、`OpError`、workflow extension methods 或 `TryInvoke`。
- 不引入 source generator、VSIX、reflection 或 dynamic code。
- 第一版不做跨方法 guard/helper 追踪。
- 第一版不做 code fix。

## 包结构

Runtime project 继续产出 `RokyZevon.OpResult` 包。Analyzer project 产出 analyzer DLL，并由 runtime package 的 pack 配置把该 DLL 放入标准 analyzer asset 路径。

预期 nupkg 资产形态：

```text
lib/net6.0/OpResult.dll
lib/net8.0/OpResult.dll
lib/net10.0/OpResult.dll
analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll
README.md
```

约束：

- Runtime assembly namespace 保持 `OpResult`。
- Runtime assembly 不引用 `Microsoft.CodeAnalysis.*`。
- Analyzer project 优先 target `netstandard2.0`，除非实现阶段证明需要并记录例外。
- Analyzer project 的 `Microsoft.CodeAnalysis.*` 依赖必须是私有 build-time dependency，例如 `PrivateAssets="all"`。
- Analyzer DLL 只作为 analyzer asset 进入 `RokyZevon.OpResult` 包，不进入 `lib/`、`ref/` 或 `runtime/`。

## 第一版诊断范围

第一版 analyzer 只报告明确语义错误。规则 ID 和最终标题在实现计划中固定；本 spec 先固定行为边界。

### 未证明分支的 `Value` / `Error` 读取

报告：

- 没有证明结果为 `Ok` 时读取 `result.Value`。
- 没有证明结果为 `Err` 时读取 `result.Error`。

允许：

- 同方法内 `IsOk` / `IsErr` guard。
- 否定 guard，例如 `!result.IsErr` 和 `!result.IsOk`。
- 短路组合 guard，例如 `if (result.IsOk && user.Enabled) { result.Value; }` 和 `if (user.Enabled && result.IsOk) { result.Value; }`。
- early return / loop continue guard，例如 `if (result.IsErr) return ...; result.Value` 和 loop 内 `if (result.IsErr) continue; result.Value`。
- `else` 分支中的对应访问。
- `Match` / `MatchAsync` 分支参数。
- `Then` / `ThenAsync` 的 success callback 参数。
- `OnOk` / `OnOkAsync` 的 success callback 参数。
- `OnErr` / `OnErrAsync` 的 error callback 参数。

不允许把以下调用视为后续外部代码的证明：

```csharp
result.OnOk(value => Use(value));
Use(result.Value);
```

`OnOk` / `OnErr` 是副作用 API，不改变调用点之后的控制流事实。

Analyzer 只把同一个 receiver expression 上的 guard 当作证明。`first.Cached.IsOk` 不能证明 `second.Cached.Value`，同一对象上的 sibling member 写入也不能使已证明的 `holder.Cached` guard 失效。若 guard 后存在能到达读取点的 `ref` / `out` / assignment / deconstruction 写入，则该证明失效；这包括 early-exit guard 的 continuing branch 中的写入。若写入所在路径在读取前通过 `return`、`throw` 或同一 loop iteration 的 `continue` / `break` 离开，则不使后续可达读取失效。

Analyzer 也允许同一个短路条件中后续 operand 的安全读取，例如 `if (result.IsOk && result.Value.Id > 0) { }` 与 `if (result.IsOk || result.Error.Message.Length > 0) { }`。该支持只适用于 C# 短路 `&&` / `||` 的求值顺序；如果读取前已有同一函数边界内的可达写入改变同一个 result，则 guard 证明失效，右侧 lambda / local function 中捕获的未来写入不使当前 access 前的证明失效。

Analyzer 识别显式 bool guard 写法和 property-pattern guard 写法，例如 `result.IsOk == true`、`result.IsErr is false` 与 `result is { IsOk: true }`。

集合 / indexer receiver identity 不在当前 PR 中扩展设计；带 arguments 的 property reference 视为不可跟踪，避免用 `results[0]` 的 guard 证明 `results[1]` 这类不同元素。

第一版不识别用户自定义 helper guard：

```csharp
EnsureOk(result);
Use(result.Value);
```

### 伪分支判断

报告核心不可靠分支判断：

```csharp
if (result.Value != null) { }
if (result.Value == null) { }
if (result.Error != null) { }
if (result.Error == null) { }
if (result.Error.Message == "") { }
if (result.Error.Message == string.Empty) { }
if (result.Error.Message != "") { }
if (result.Error.Message == System.String.Empty) { }
if (result.Value is not null) { }
```

原因：

- `Err` 上读取 `Value` 会得到 `default(T)`。
- `Ok` 上读取 `Error` 会得到 empty `OpError`。
- 空 message 是错误展示语义，不是 `Ok` / `Err` 分支语义。

如果已经通过 `IsErr` 证明 failure 分支，则允许对 `result.Error.Message` 做内容检查，例如 `if (result.IsErr) { if (result.Error.Message == "") { } }` 或 `if (result.IsErr) { if (result.Error.Message != "") { } }`。

第一版不承诺覆盖 `string.IsNullOrEmpty(result.Error.Message)`、`Length == 0`、局部变量传播或其它等价写法，避免误伤日志、UI 和 telemetry 场景。

### Unused `OpResult` return value

报告裸 expression statement 形式的 unused `OpResult` / `OpResult<T>` 返回值：

```csharp
SaveUser(user);
Validate(user);
OpResults.TryInvoke(() => WriteFile(path));
```

不报告：

```csharp
var result = SaveUser(user);
return SaveUser(user);
if (SaveUser(user).IsErr) return;
SaveUser(user).Match(onOk: ..., onErr: ...);
_ = SaveUser(user);
```

显式 discard assignment `_ = ...` 表示调用方有意丢弃返回值；第一版不报告。

第一版不专门报告 `Task<OpResult>` / `Task<OpResult<T>>` 未 await 的场景，避免和 C# task diagnostics 重叠。

### 直接错误链重建

报告在失败分支中直接从 `result.Error.Message` 重建 `OpError` / failed result 的模式：

```csharp
if (result.IsErr)
{
    return OpResults.Err(result.Error.Message);
}
```

如果调用 nullable-inner overload 但显式传入 null inner error，例如 `OpResults.Err(result.Error.Message, null)` 或 `OpResults.Err(innerError: null, message: result.Error.Message)`，也按同一类直接 message rebuild 处理。Analyzer 按目标参数匹配 `message` / `innerError`，不依赖 named argument 的源码顺序。

推荐写法：

```csharp
if (result.IsErr)
{
    return result.Error.ToErr("Could not load profile.");
}
```

或：

```csharp
if (result.IsErr)
{
    return OpResults.Err("Could not load profile.", result.Error);
}
```

第一版不追踪局部变量、字符串插值、helper 方法或跨方法数据流：

```csharp
var message = result.Error.Message;
return OpResults.Err(message);

return OpResults.Err($"Failed: {result.Error.Message}");

return CreateFailure(result.Error.Message);
```

第一版不展开 null-forgiving suppression，例如 `OpResults.Err(result.Error!.Message)` 暂不作为 `OPRESULT005` 匹配范围。

这些场景可能丢失错误链，但需要更强数据流分析，留给后续规则。

## Severity 和配置

第一版 diagnostics 默认 severity 为 `warning`。

消费者可以用 `.editorconfig` 调整：

```ini
[*.cs]
dotnet_diagnostic.OPRESULT001.severity = error
dotnet_diagnostic.OPRESULT002.severity = none
```

README 应说明：

- Analyzer diagnostics 是默认语义保护。
- 推荐先修正代码，而不是关闭规则。
- 如需在特定代码库中调整规则，应使用标准 diagnostic 配置。

## 项目布局建议

保持当前仓库布局，最小化迁移：

```text
OpResult/
  OpResult.csproj
  ...runtime files...

OpResult.Analyzers/
  OpResult.Analyzers.csproj
  ...analyzer files...

OpResult.Tests/
  ...runtime tests...

OpResult.Analyzers.Tests/
  ...analyzer unit tests...

OpResult.Package.Tests/
  ...packed package fixture tests...
```

不进行 `src/` / `tests/` 大迁移，除非后续单独批准。

## 文档要求

README 安装段落保持单包安装：

````markdown
## Installation

```bash
dotnet add package RokyZevon.OpResult
```

The package includes the runtime library and the default Roslyn analyzers.
````

README 需要新增 analyzer diagnostics 章节，至少包含：

- 规则 ID。
- 规则标题。
- 默认 severity。
- bad example。
- good example。
- `.editorconfig` 调整方式。

README 不应再提 runtime-only 包、analyzer-only 包或 meta package。

## 测试要求

### Runtime tests

- 现有 runtime tests 继续通过。
- `OpResult<T>`、`OpError.InnerError`、`Then` / `ThenAsync`、`Match` / `MatchAsync`、`OnOk` / `OnErr`、`TryInvoke` 行为不因 analyzer 引入而变化。

### Analyzer unit tests

- 覆盖每类第一版 diagnostic。
- 覆盖允许的 guard、early return、else、branch API callback 参数。
- 覆盖 nullable disabled source，确认 analyzer 不依赖 nullable reference types。
- 覆盖 value-type payload 的未证明 `Value` 读取。
- 覆盖 non-diagnostic cases，避免把 `OnOk` / `OnErr` 调用误判为后续外部证明。

### Package fixture tests

必须 pack 本地 `RokyZevon.OpResult` 包，并用 fixture project 通过 local package source restore。

必要场景：

1. 只安装 `RokyZevon.OpResult` 后，runtime API 可用。
2. 只安装 `RokyZevon.OpResult` 后，已知 analyzer violation 在 `dotnet build` 中产生预期 diagnostic。
3. nupkg 包含 runtime assets 和 `analyzers/dotnet/cs/RokyZevon.OpResult.Analyzers.dll`。
4. nupkg 不把 analyzer DLL 放入 `lib/`、`ref/` 或 `runtime/`。
5. runtime compile graph 不暴露 `Microsoft.CodeAnalysis.*` 作为消费者 runtime dependency。

Package fixture tests 是必须项，因为 analyzer 分发是 build-time 行为，不能只靠 analyzer unit tests 或 IDE 验证。

Package fixture tests 验证最终 Release package 形态，必须以 Release 配置运行。Debug 或 IDE 默认配置下不应尝试打包旧的 Release 输出，而应明确失败并提示使用 `dotnet test OpResult.Package.Tests/OpResult.Package.Tests.csproj -c Release`。

## CI 要求

CI 至少执行：

```bash
dotnet restore OpResult.slnx
dotnet build OpResult.slnx -c Release --no-restore
dotnet test OpResult.slnx -c Release --no-build --no-restore
dotnet pack OpResult/OpResult.csproj -c Release --no-build --no-restore -o artifacts/packages
# build package fixture projects from artifacts/packages
```

精确命令可在实现计划中根据最终项目布局调整。

## 验收标准

设计实现完成时必须满足：

- `dotnet add package RokyZevon.OpResult` 提供 runtime API。
- 同一个包默认启用 OpResult analyzer diagnostics。
- Analyzer diagnostics 出现在 `dotnet build`，不只出现在 IDE。
- nupkg 中 runtime assembly 和 analyzer assembly 资产位置正确。
- Runtime assembly 不引用 Roslyn。
- 第一版 diagnostics 只覆盖本 spec 定义的语义错误，不扩张为风格规则。
- 默认 severity 为 `warning`。
- 不发布 `RokyZevon.OpResult.Core` 或 `RokyZevon.OpResult.Analyzers` 包。
- README 不再描述三包、runtime-only 或 analyzer-only 安装方式。
