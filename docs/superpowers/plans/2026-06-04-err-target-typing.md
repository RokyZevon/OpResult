# Err Target Typing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore `OpResults.Err(...)` as an `OpError` factory and let it target-type into `OpResult` / `OpResult<T>` through implicit conversions.

**Architecture:** `OpError` remains the error detail object. `OpResults.Err(string?)` returns `OpError`; `OpResult` and `OpResult<T>` own the implicit conversion from `OpError`, matching the existing `T -> OpResult<T>` target-type conversion style. `Err<T>` remains available in `v0.1.1` for compatibility and is not marked obsolete.

**Tech Stack:** C# / .NET `net10.0;net6.0`, xUnit v3 tests, Roslyn compile-only nullable tests, Markdown docs.

---

## File Structure

- Modify: `OpResult/OpResults.cs`
- Modify: `OpResult/OpResult.cs`
- Modify: `OpResult.Tests/OpResultTests.cs`
- Modify: `OpResult.Tests/NullableFlowCompilationTests.cs`
- Modify: `OpResult.Tests/DirectWorkflowSyntaxTests.cs`
- Modify: `README.md`, `README.zh.md`
- Modify: `OpResult/OpResult.csproj`
- Modify: `OpResult.Tests/PackageMetadataTests.cs`
- Create: `docs/superpowers/specs/2026-06-04-err-target-typing-design.md`
- Modify: `docs/superpowers/specs/2026-05-17-opresult-api-design.md`

## Task 1: Spec And Tests First

- [ ] Create `docs/superpowers/specs/2026-06-04-err-target-typing-design.md` with the v0.1.1 Err target-typing decisions.
- [ ] Update `OpResults_FactorySurfaceMatchesSpec` so non-generic `Err` returns `OpError`.
- [ ] Replace the negative OpError conversion test with positive conversion and null-guard tests.
- [ ] Add compile-only tests for `OpResult<T> Get() => OpResults.Err("...")`.
- [ ] Add direct workflow syntax coverage for generic `Err()` target typing.
- [ ] Run `dotnet build OpResult.slnx -c Release` and verify the expected failing-test state before implementation.

## Task 2: Implement Err Factory And Conversions

- [ ] Change `OpResults.Err(string?)` to return `OpError`.
- [ ] Add `OpError -> OpResult` implicit conversion to `OpResult`.
- [ ] Add `OpError -> OpResult<T>` implicit conversion to `OpResult<T>`.
- [ ] Null `OpError` conversions throw `ArgumentNullException` with parameter name `error`.
- [ ] Run focused tests for `OpResultTests`, `NullableFlowCompilationTests`, and `DirectWorkflowSyntaxTests`.

## Task 3: Update Internal Usage And Docs

- [ ] Simplify internal generic failure forwarding where target typing makes the intent clearer.
- [ ] Keep `Err<T>` in `TryInvoke<T>` / `TryInvokeAsync<T>` if it improves local clarity.
- [ ] Update README examples to prefer `OpResults.Err(...)`.
- [ ] Update README.zh with the same semantics.
- [ ] Add a short supersede note to the v0.1.0 core API spec.
- [ ] Update project metadata and package metadata tests from `0.1.0` to `0.1.1`.

## Task 4: Final Verification

- [ ] Run `dotnet build OpResult.slnx -c Release`.
- [ ] Run `dotnet test OpResult.Tests/OpResult.Tests.csproj -c Release`.
- [ ] Run `dotnet pack OpResult/OpResult.csproj -c Release -o artifacts/packages -p:PackageVersion=0.1.1 -p:Version=0.1.1`.
- [ ] Confirm `Err<T>` remains available and is not obsolete.

## Assumptions

- Release target is `0.1.1`.
- `Err<T>` removal is deferred to a future `0.2.0` decision.
- This plan does not include git write commands because repository instructions forbid git write operations.
