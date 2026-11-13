# Synchronous `Then` usage requirements

[简体中文](./then-chaining.zh.md)

This document defines consumption and migration requirements for the four synchronous `Then` overloads. It does not change their runtime behavior or define requirements for `ThenAsync`.

## 1. Requirement language

Uppercase **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** use the requirement levels defined by [RFC 2119](https://www.rfc-editor.org/rfc/rfc2119) and [RFC 8174](https://www.rfc-editor.org/rfc/rfc8174):

| Keywords | Meaning |
| --- | --- |
| **MUST / MUST NOT** | An absolute requirement or prohibition within the stated scope. |
| **SHOULD / SHOULD NOT** | A recommendation to follow or avoid a practice; justified exceptions require understanding and weighing the consequences. |
| **MAY** | A permitted option. |

Only uppercase keywords carry these meanings. Sections 2–4 contain the usage requirements. Examples and diagnostic configuration are explanatory and introduce no additional requirements.

## 2. Composition

1. When each step only needs the preceding successful value, callers **SHOULD** use a flat `.Then(...).Then(...)` chain.
2. An anonymous success callback passed to a synchronous `Then` **SHOULD NOT** directly return another synchronous `Then` chain.
3. When later steps need an earlier value or local error handling, callers **SHOULD** extract a named business step with an explicit return type.
4. Callers **SHOULD NOT** use `OnOk` to copy a value into an outer variable solely to pass it to a later step.

## 3. Behavior-preserving migration

The following requirements apply when a rewrite is intended to preserve existing behavior:

1. The rewrite **MUST** retain success values and preserve the original `OpError` instance wherever the existing workflow propagates an error unchanged.
2. The rewrite **MUST NOT** change callback order, execution conditions, or the timing of side effects. In particular, a step skipped after an `Err` remains skipped.
3. The rewrite **MUST** retain the error scope of each `OnErr` handler and the recovery behavior of any `Match` call.
4. The rewrite **MUST** retain resource lifetimes and cleanup behavior on success, failure, and exception.

## 4. Exceptions

Callers **MAY** retain a nested chain when a specific requirement justifies it, such as preserving value capture, local error handling, or resource lifetime. Before choosing this exception, callers **SHOULD** consider whether a named business step can preserve the same behavior.

When suppressing the warning locally, callers **SHOULD** document the reason.

## 5. Examples

The examples omit business method and type definitions.

### 5.1. Flat composition

Not recommended:

```csharp
OpResult<int> IncreaseTwice(int value) =>
    OpResults.Ok(value).Then(first =>
        OpResults.Ok(first + 1)
            .Then(second => OpResults.Ok(second + 1)));
```

Prefer a flat chain:

```csharp
OpResult<int> IncreaseTwice(int value) =>
    OpResults.Ok(value)
        .Then(first => OpResults.Ok(first + 1))
        .Then(second => OpResults.Ok(second + 1));
```

### 5.2. Keeping an earlier value

If `CheckPermission` returns a non-generic `OpResult`, it does not carry the loaded `Order` into the next step. The direct chain `LoadOrder().Then(CheckPermission).Then(SaveOrder)` therefore cannot pass that value to `SaveOrder`.

A named step keeps the `Order` available to both operations:

```csharp
OpResult SaveAfterPermission(Order order) =>
    CheckPermission(order).Then(() => SaveOrder(order));

OpResult UpdateOrder(int id) =>
    LoadOrder(id).Then(SaveAfterPermission);
```

### 5.3. Local error handling

An `OnErr` attached inside a continuation observes errors from that child chain. It does not observe an error from the result that invokes the continuation, because an upstream `Err` skips the continuation entirely:

```csharp
OpResult UpdateOrder() =>
    LoadOrder().Then(order =>
        CheckPermission(order)
            .Then(() => SaveOrder(order))
            .OnErr(Log));
```

Extracting the child chain and its handler together into a named step preserves this error scope. Moving the handler to the outer chain would also log load errors.

### 5.4. Resource lifetime

Keeping the chain inside the resource scope makes the resource available to both steps:

```csharp
using var resource = AcquireResource();

return OpResults.Ok()
    .Then(() => WriteFirstPart(resource))
    .Then(() => WriteSecondPart(resource));
```

The resource is released when this scope exits, including after an error or exception. Extracting a step outside the original `using` or `try`/`finally` scope can change its lifetime.

## 6. Diagnostic and configuration

`OPRESULT006` defaults to **Warning**. It reports a synchronous `Then` whose anonymous continuation directly returns a chain containing another synchronous `Then`, within the following analysis boundaries. Two nested levels are sufficient; explicit generic type arguments do not exempt the chain.

- Calls are identified by their bound OpResult methods. The analyzer follows returned chains through `Then`, `OnOk`, and `OnErr` receivers, parentheses, identity conversions, and the value branches of conditional and switch expressions.
- Only values returned by the anonymous continuation itself connect its chain. The analyzer does not track nesting through other method calls, local functions, delegate variables, local result aliases, `Match`, custom wrappers, or user-defined conversions. Observation callbacks and independent side branches are not joined to the returned chain; nested structures inside them are checked independently.
- Flat receiver chains do not add nesting. Each connected nested structure produces one diagnostic on its outermost `Then` name; independent structures are reported separately.
- Generated code and `ThenAsync` are outside this rule's scope. No automatic fix is provided.

The diagnostic reports this nested call shape; it does not verify every requirement in this document or decide whether an exception is justified. Its severity is independent of the requirement levels above.

If the project treats warnings as errors, `OPRESULT006` can fail the build until the chain is migrated or the diagnostic is configured. Change only this rule in `.editorconfig` to keep it visible as a suggestion:

```ini
[*.cs]
dotnet_diagnostic.OPRESULT006.severity = suggestion
```

To disable only this rule:

```ini
[*.cs]
dotnet_diagnostic.OPRESULT006.severity = none
```

For one intentional case, use a local pragma and restore the diagnostic afterward:

```csharp
#pragma warning disable OPRESULT006 // Keep the nested shape for this specific workflow.
var result = LoadOrder().Then(order =>
    CheckPermission(order).Then(() => SaveOrder(order)));
#pragma warning restore OPRESULT006
```
