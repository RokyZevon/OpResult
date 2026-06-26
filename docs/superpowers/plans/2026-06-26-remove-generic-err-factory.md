# Remove Generic Err Factory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the obsolete public `OpResults.Err<T>(string?)` factory and migrate current code to target-typed `OpResults.Err(...)`.

**Architecture:** `OpResults.Err(string?)` remains the single public failed-result factory and returns `OpError`. `OpResult` and `OpResult<T>` keep their `OpError` implicit conversions, so value-result failure construction is expressed by the target type instead of a generic factory call.

**Tech Stack:** C#/.NET multi-target library, xUnit v3 tests, Roslyn analyzer tests, Markdown docs.

---

## File Structure

- Modify: `OpResult/OpResults.cs` to delete the public generic `Err<T>` factory and update `TryInvoke<T>` null branches.
- Modify: `OpResult.Tests/OpResultTests.cs` to assert that the public factory surface has no generic Err method.
- Modify: `OpResult.Tests/NullableFlowCompilationTests.cs` and `OpResult.Tests/WorkflowTests.cs` to use target-typed failed value results.
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs` to remove generic Err fixture code while preserving analyzer coverage.
- Modify: `README.md` and `README.zh.md` to remove current compatibility guidance for `Err<T>`.
- Add: `docs/superpowers/specs/2026-06-26-remove-generic-err-factory-design.md`.
- Add: `docs/superpowers/plans/2026-06-26-remove-generic-err-factory.md`.

## Task 1: Public API Test First

**Files:**
- Modify: `OpResult.Tests/OpResultTests.cs`

- [ ] **Step 1: Update factory-surface test to reject generic Err**

Replace `OpResults_FactorySurfaceMatchesSpec` with a version that checks the remaining public factories and asserts no public generic `Err` method exists:

```csharp
[Fact]
public void OpResults_FactorySurfaceMatchesSpec()
{
    var nonGenericType = GetNonGenericOpResultType();

    var okWithoutValue = FindOpResultsFactoryMethod(
        nameof(OpResults.Ok),
        parameterCount: 0,
        genericArity: 0,
        method => method.ReturnType == nonGenericType);

    var okWithValue = FindOpResultsFactoryMethod(
        nameof(OpResults.Ok),
        parameterCount: 1,
        genericArity: 1,
        method => IsOpResultOfMethodGenericParameter(method.ReturnType, method.GetGenericArguments().Single()));

    var errWithoutValue = FindOpResultsFactoryMethod(
        nameof(OpResults.Err),
        parameterCount: 1,
        genericArity: 0,
        method => method.ReturnType == typeof(OpError));

    var errWithInnerError = FindOpResultsFactoryMethod(
        nameof(OpResults.Err),
        parameterCount: 2,
        genericArity: 0,
        method => method.ReturnType == typeof(OpError));

    var genericErrFactories = typeof(OpResults)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Where(method =>
            method.Name == nameof(OpResults.Err) &&
            method.GetGenericArguments().Length > 0)
        .ToArray();

    var okValueParameter = okWithValue.GetParameters().Single();
    var errMessageParameter = errWithoutValue.GetParameters().Single();
    var innerErrorParameters = errWithInnerError.GetParameters();

    Assert.NotNull(okWithoutValue);
    Assert.Equal(typeof(OpError), errWithoutValue.ReturnType);
    Assert.Equal(typeof(OpError), errWithInnerError.ReturnType);
    Assert.Empty(genericErrFactories);
    Assert.True(HasDisallowNullAttribute(okValueParameter));
    Assert.True(IsNullableAnnotated(errMessageParameter));
    Assert.True(IsNullableAnnotated(innerErrorParameters[0]));
    Assert.True(IsNullableAnnotated(innerErrorParameters[1]));
}
```

- [ ] **Step 2: Verify the new test fails for the right reason**

Run:

```bash
dotnet build OpResult.Tests/OpResult.Tests.csproj -c Release --no-restore -m:1 -nr:false -v:minimal
dotnet test OpResult.Tests/OpResult.Tests.csproj -c Release --no-build --no-restore -- --filter-method OpResult.Tests.OpResultTests.OpResults_FactorySurfaceMatchesSpec
```

Expected: FAIL because `genericErrFactories` still contains `OpResults.Err<T>(string?)`.

## Task 2: Remove Generic Err Factory And Migrate Calls

**Files:**
- Modify: `OpResult/OpResults.cs`
- Modify: `OpResult.Tests/NullableFlowCompilationTests.cs`
- Modify: `OpResult.Tests/WorkflowTests.cs`
- Modify: `OpResult.Analyzers.Tests/OpResultUsageAnalyzerTests.cs`

- [ ] **Step 1: Delete public `Err<T>` and update internal returns**

Remove this method from `OpResult/OpResults.cs`:

```csharp
public static OpResult<T> Err<T>(string? message)
    where T : notnull =>
    OpResult<T>.Err(OpError.New(message));
```

Change value-returning `TryInvoke` null branches from:

```csharp
return value is null ? Err<T>(NullOperationResultMessage) : Ok(value);
return Err<T>(NullOperationResultMessage);
```

to:

```csharp
return value is null ? Err(NullOperationResultMessage) : Ok(value);
return Err(NullOperationResultMessage);
```

- [ ] **Step 2: Migrate current test and analyzer fixture calls**

Replace current `OpResults.Err<T>(...)` call sites with target-typed assignments or arguments. Examples:

```csharp
OpResult<int> errForValueToValue = OpResults.Err("value-to-value short-circuit");
```

```csharp
var diagnostics = CompileSnippet(
    """
    OpResult<User> result = OpResults.Err("failed");
    if (!result.IsErr) return;
    var message = result.Error.Message;
    _ = message;
    """);
```

```csharp
OpResult<User> wrapped = OpResults.Err(result.Error.Message);
```

For method arguments where the generic type is supplied by reflection helper type parameters, cast or type the argument explicitly:

```csharp
InvokeWorkflow<string>(
    matchValueFold,
    (OpResult<int>)OpResults.Err("value-fold"),
    onOkValueFold,
    onErrValueFold);
```

- [ ] **Step 3: Verify source and tests compile**

Run:

```bash
dotnet test OpResult.Tests/OpResult.Tests.csproj -c Release --no-build --no-restore -- --filter-method OpResult.Tests.OpResultTests.OpResults_FactorySurfaceMatchesSpec
```

Expected: PASS.

Run:

```bash
dotnet test OpResult.Tests/OpResult.Tests.csproj -c Release --no-build --no-restore
```

Expected: PASS.

Run:

```bash
dotnet build OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release --no-restore -m:1 -nr:false -v:minimal
dotnet test OpResult.Analyzers.Tests/OpResult.Analyzers.Tests.csproj -c Release --no-build --no-restore
```

Expected: PASS.

## Task 3: Clean Current Documentation

**Files:**
- Modify: `README.md`
- Modify: `README.zh.md`

- [ ] **Step 1: Remove current compatibility guidance**

Delete the English sentence:

```markdown
`OpResults.Err<T>(...)` remains available as an explicit compatibility form, but new code should prefer `OpResults.Err(...)` and let the target result type perform the conversion.
```

Delete the Chinese sentence:

```markdown
`OpResults.Err<T>(...)` 仍作为显式兼容写法保留；新代码推荐使用 `OpResults.Err(...)`，由目标 result 类型完成转换。
```

- [ ] **Step 2: Verify current docs no longer recommend generic Err**

Run:

```bash
rg -n "OpResults\\.Err<|Err<T>|兼容写法保留|compatibility form" README.md README.zh.md
```

Expected: no matches.

## Task 4: Final Verification

**Files:**
- All changed files

- [ ] **Step 1: Check current non-historical code for generic Err calls**

Run:

```bash
rg -n "OpResults\\.Err<" OpResult OpResult.Tests OpResult.Analyzers OpResult.Analyzers.Tests OpResult.Package.Tests README.md README.zh.md
```

Expected: no matches.

- [ ] **Step 2: Run full solution tests**

Build first, then run tests without forwarding MSBuild switches to the Microsoft.Testing.Platform runner:

```bash
dotnet build OpResult.slnx -c Release --no-restore -m:1 -nr:false -v:minimal
dotnet test OpResult.slnx -c Release --no-build --no-restore
```

Expected: PASS.

- [ ] **Step 3: Inspect git status**

Run:

```bash
git status --short
```

Expected: only the planned source, test, README, spec, and plan files changed.
