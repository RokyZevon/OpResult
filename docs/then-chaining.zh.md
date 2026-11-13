# 同步 `Then` 使用规范

[English](./then-chaining.md)

本文规定四个同步 `Then` 重载的消费方式和迁移要求，不改变其运行时行为，也不规定 `ThenAsync` 的用法。

## 1. 规范用语

大写的 **MUST**、**MUST NOT**、**SHOULD**、**SHOULD NOT** 和 **MAY** 采用 [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119) 与 [RFC 8174](https://www.rfc-editor.org/rfc/rfc8174) 定义的要求等级：

| 关键词 | 含义 |
| --- | --- |
| **MUST / MUST NOT** | 在所述适用范围内必须遵守的要求或禁止事项。 |
| **SHOULD / SHOULD NOT** | 应当遵循或避免的做法；在理解并权衡后果后，允许有充分理由的例外。 |
| **MAY** | 允许选择的做法。 |

仅大写关键词具有上述含义。第 2–4 节为使用规范；示例和诊断配置用于解释，不增加额外要求。

## 2. 链式组合

1. 每一步只依赖前一步的成功值时，调用方 **SHOULD** 使用 `.Then(...).Then(...)` 平链。
2. 传给同步 `Then` 的匿名成功回调 **SHOULD NOT** 直接返回另一条同步 `Then` 链。
3. 后续步骤需要较早的值或局部错误处理时，调用方 **SHOULD** 提取具有显式返回类型的具名业务步骤。
4. 调用方 **SHOULD NOT** 仅为向后续步骤传值，就通过 `OnOk` 把该值保存到外部变量。

## 3. 保持行为不变的迁移

以下要求适用于以保持现有行为不变为目标的改写：

1. 改写 **MUST** 保留成功值，并在原流程将错误原样向后传递的路径上保留同一个 `OpError` 实例。
2. 改写 **MUST NOT** 改变回调顺序、执行条件或副作用发生的时机；原本因 `Err` 而跳过的步骤仍然不执行。
3. 改写 **MUST** 保留每个 `OnErr` handler 的错误处理范围，以及已有 `Match` 调用的恢复行为。
4. 改写 **MUST** 保留资源生命周期，以及成功、失败和异常路径上的清理行为。

## 4. 例外

存在明确需求时，例如需要保留值捕获、局部错误处理或资源生命周期，调用方 **MAY** 保留嵌套链。选择这一例外前，调用方 **SHOULD** 先考虑能否通过具名业务步骤保留相同行为。

局部抑制警告时，调用方 **SHOULD** 说明原因。

## 5. 示例

以下示例省略了业务方法和类型定义。

### 5.1. 扁平串联

不推荐：

```csharp
OpResult<int> IncreaseTwice(int value) =>
    OpResults.Ok(value).Then(first =>
        OpResults.Ok(first + 1)
            .Then(second => OpResults.Ok(second + 1)));
```

推荐扁平链：

```csharp
OpResult<int> IncreaseTwice(int value) =>
    OpResults.Ok(value)
        .Then(first => OpResults.Ok(first + 1))
        .Then(second => OpResults.Ok(second + 1));
```

### 5.2. 保留较早的值

如果 `CheckPermission` 返回非泛型 `OpResult`，它不会把加载出的 `Order` 继续传给下一步。因此，直接写成 `LoadOrder().Then(CheckPermission).Then(SaveOrder)` 无法把该值传给 `SaveOrder`。

具名步骤让两个操作都能使用传入的 `Order`：

```csharp
OpResult SaveAfterPermission(Order order) =>
    CheckPermission(order).Then(() => SaveOrder(order));

OpResult UpdateOrder(int id) =>
    LoadOrder(id).Then(SaveAfterPermission);
```

### 5.3. 局部错误处理

放在 continuation 内的 `OnErr` 只观察子链的错误。它不会观察调用这个 continuation 的上游结果错误，因为上游 `Err` 会跳过整个 continuation：

```csharp
OpResult UpdateOrder() =>
    LoadOrder().Then(order =>
        CheckPermission(order)
            .Then(() => SaveOrder(order))
            .OnErr(Log));
```

把子链和它的 handler 一起提取为具名步骤，可以保留该错误处理范围。将 handler 移到外层链上，会额外记录加载错误。

### 5.4. 资源生命周期

调用链位于资源作用域内，两个步骤都可以使用该资源：

```csharp
using var resource = AcquireResource();

return OpResults.Ok()
    .Then(() => WriteFirstPart(resource))
    .Then(() => WriteSecondPart(resource));
```

资源在离开该作用域时释放，包括返回错误或抛出异常的情况。将步骤提取到原来的 `using` 或 `try`/`finally` 作用域之外，可能改变资源生命周期。

## 6. 诊断与配置

`OPRESULT006` 默认级别为 **Warning**。当同步 `Then` 的匿名 continuation 直接返回的链中包含另一个同步 `Then`，且符合以下分析边界时，该规则报告诊断。两层嵌套即可触发，显式泛型实参也不豁免。

- 按绑定到的 OpResult 方法识别调用，沿返回链中 `Then`、`OnOk`、`OnErr` 的接收者、括号、恒等转换，以及条件表达式和 switch 表达式的值分支检查。
- 只用该匿名 continuation 自身返回的值连接调用链。不跨其他方法调用、局部函数、委托变量、局部结果别名、`Match`、自定义包装或用户定义转换追踪嵌套关系。观察回调和独立旁支不并入返回主链；其中独立存在的嵌套结构仍单独检查。
- 平铺的接收者链不增加嵌套层级。每个连续嵌套结构仅在最外层 `Then` 名称处报告一次，独立结构分别报告。
- 生成代码和 `ThenAsync` 不在本规则范围内；该规则不提供自动修复。

该诊断只报告上述嵌套调用形状，不验证本文的全部要求，也不判断例外理由是否成立。诊断严重级别与上述规范要求等级相互独立。

如果项目把 warning 当成 error，迁移或配置诊断之前，`OPRESULT006` 可能导致构建失败。可以在 `.editorconfig` 中只把这一条规则降为 suggestion：

```ini
[*.cs]
dotnet_diagnostic.OPRESULT006.severity = suggestion
```

如果只想关闭这一条规则：

```ini
[*.cs]
dotnet_diagnostic.OPRESULT006.severity = none
```

对于一个有意保留的情况，可以局部关闭并在后面恢复诊断：

```csharp
#pragma warning disable OPRESULT006 // 在这个特定流程中保留嵌套形状。
var result = LoadOrder().Then(order =>
    CheckPermission(order).Then(() => SaveOrder(order)));
#pragma warning restore OPRESULT006
```
