# Remove Generic Err Factory Design

## Background

The 2026-06-04 Err target-typing design restored `OpResults.Err(string?)` as an `OpError` factory and added implicit conversions from `OpError` to `OpResult` and `OpResult<T>`. That design kept `OpResults.Err<T>(string?)` for v0.1.1 source compatibility and deferred removal to a future decision.

This design is that future decision. The generic `OpResults.Err<T>(...)` factory is now obsolete for this codebase and should be removed from the public API and from current source, tests, and README examples.

## Goals

- Delete the public `OpResults.Err<T>(string?)` factory.
- Use `OpResults.Err(string?)` plus target-typed conversion for all failed value-result construction.
- Keep `OpError` as the only public error factory return type.
- Keep `OpError -> OpResult` and `OpError -> OpResult<T>` implicit conversions.
- Remove current README guidance that describes `OpResults.Err<T>(...)` as a compatibility form.
- Preserve historical specs and plans as historical records, while this document supersedes their compatibility decision.

## Non-Goals

- Do not remove the internal `OpResult<T>.Err(OpError)` helper. It is not the obsolete public factory and is used to preserve exact `OpError` instances during short-circuiting.
- Do not change `OpResult<T>` success-value nullability, guard semantics, or default-result semantics.
- Do not add new overloads such as `OpResults.Err<T>(string?, OpError?)`.
- Do not rewrite historical design documents except where a new superseding note is needed.

## Public API

The public factory surface becomes:

```csharp
public static OpResult Ok();

public static OpResult<T> Ok<T>(T? value)
    where T : notnull;

public static OpError Err(string? message);

public static OpError Err(string? message, OpError? innerError);
```

Generic failed results are created through target typing:

```csharp
OpResult<User> FindUser(Guid id) =>
    OpResults.Err("User was not found.");
```

The direct `OpError` conversion remains invalid for null error objects and throws `ArgumentNullException` at the conversion boundary.

## Implementation Notes

`TryInvoke<T>` and `TryInvokeAsync<T>` should return the non-generic `Err(...)` factory in null-result branches. Their declared return type supplies the generic result target type.

Workflow tests and analyzer fixtures should avoid generic Err calls. Where a test needs an Err `OpResult<T>` local, declare the local type explicitly:

```csharp
OpResult<int> result = OpResults.Err("failed");
```

The analyzer's direct-error-chain-loss coverage should still verify value-result construction, but it should assign the non-generic factory result into an `OpResult<User>` instead of invoking the deleted generic factory.

## Acceptance Criteria

- `OpResults_FactorySurfaceMatchesSpec` proves no public generic `Err<T>(string?)` method exists.
- Compile-only nullable-flow tests still prove target-typed `Err(...)` works for `OpResult<T>`.
- Analyzer tests still detect direct error-chain loss for `OpResults.Err(result.Error.Message)` when assigned to an `OpResult<T>`.
- Current source, tests, and README files contain no `OpResults.Err<...>` calls and no current guidance recommending `Err<T>`.
- `dotnet test OpResult.slnx -c Release` passes.
