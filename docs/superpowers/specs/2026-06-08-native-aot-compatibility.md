# Native AOT 兼容性设计

## 设计目标

OpResult 必须保持 Native AOT-compatible，并在支持 AOT 分析的 target framework 上暴露官方 MSBuild metadata。

该约束独立于具体 API 功能 spec。核心 Result API、TryInvoke API 和后续新增 API 都必须遵守本 spec。

## 项目配置

类库项目必须：

- 保留面向支持 AOT 分析 target framework 的资产。
- 可同时保留旧 target framework，但旧资产不需要携带 AOT metadata。
- 对支持 AOT 分析的 target framework 条件式设置 `IsAotCompatible=true`。
- 在相同条件下设置 `VerifyReferenceAotCompatibility=true`。

`IsAotCompatible` 用于启用 .NET SDK 的 AOT、trimming 和 single-file analyzers。`VerifyReferenceAotCompatibility` 用于检查引用程序集是否带有兼容性 metadata。

## 实现约束

生产代码必须保持静态可分析：

- 不使用运行时反射发现或调用成员。
- 不使用 `dynamic`。
- 不使用运行时代码生成、`Reflection.Emit` 或表达式树编译。
- 不使用未标注的反射式序列化。
- 不调用会触发 `RequiresDynamicCode` 或 `RequiresUnreferencedCode` 的 API。
- 新增 package reference 前必须确认该依赖声明 AOT 兼容，或用明确设计说明解释为什么安全。

测试代码可以使用反射验证 public API surface，但测试反射不能进入生产代码。

## 文档要求

README 和 README.zh 应简要说明：

- 包声明 Native AOT support。
- `IsAotCompatible` 会启用 AOT、trimming 和 single-file analyzers。
- `VerifyReferenceAotCompatibility` 会检查引用程序集兼容性 metadata。
- 库实现避免反射、`dynamic`、运行时代码生成和外部依赖。

README 不应列出具体 target framework 版本；这些版本由项目文件和测试保证。

## 验收标准

测试至少覆盖：

- 项目 target framework 列表包含支持 AOT 分析的资产和旧兼容资产。
- `IsAotCompatible` 存在，值为 `true`，且带有 target framework 条件。
- `VerifyReferenceAotCompatibility` 存在，值为 `true`，且条件与 `IsAotCompatible` 一致。
- package description 和 tags 暴露 Native AOT / trimming 信号。

实现后必须通过：

```bash
dotnet build OpResult.slnx -c Release
dotnet run --project OpResult.Tests -c Release --no-build
```
