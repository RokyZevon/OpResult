# OpResult

OpResult is a lightweight .NET Result Pattern library for representing business operations with explicit `Ok` and `Err` paths, without modeling expected business failures as exceptions.

## Core Types

The current release has two core types:

- `OpResult<T>` carries a `T` value when successful and an `OpError` when failed.
- `OpError` is the fixed error type and exposes `Message`.

## Creating Results

Return a successful result directly from a value, or create one explicitly with `OpResults.Ok(...)`:

```csharp
OpResult<int> GetCount()
{
    return 42;
}

OpResult<int> GetExplicitCount()
{
    return OpResults.Ok(42);
}
```

Create failures with `OpResults.Err(...)`. The returned `OpError` converts to any `OpResult<T>`, so error returns stay concise:

```csharp
OpResult<int> GetCount()
{
    return OpResults.Err("count failed");
}
```

Successful values follow a non-null contract. Nullable-enabled callers should get compile-time warnings when passing null to `OpResults.Ok(...)` or directly converting a nullable reference into `OpResult<T>`. If that warning is explicitly bypassed, the Ok construction path throws `ArgumentNullException` instead of creating a successful result with a null payload.

Null or whitespace error messages passed to `OpResults.Err(...)` are normalized to an empty message.

## Reading Branches

Use `IsOk` and `IsErr` to check the current branch before consuming `Value` or `Error`:

```csharp
OpResult<string> result = LoadName();

if (result.IsOk)
{
    Console.WriteLine(result.Value);
}

if (result.IsErr)
{
    Console.WriteLine(result.Error.Message);
}
```

Reading `Value` from a failed result returns the default value of the success type. Reading `Error` from a successful result returns an empty-message error. `default(OpResult<T>)` is a failed result with an empty error message.

## Chaining

Use `Then` to continue with another operation that can fail. Failed results short-circuit and propagate the current error:

```csharp
OpResult<Order> CreateOrder(Guid userId)
{
    return LoadUser(userId)
        .Then(ValidateUser)
        .Then(BuildOrder);
}
```

Asynchronous workflows use `Task<OpResult<T>>` with `ThenAsync`:

```csharp
Task<OpResult<Order>> CreateOrderAsync(Guid userId)
{
    return LoadUserAsync(userId)
        .ThenAsync(ValidateUserAsync)
        .ThenAsync(BuildOrderAsync);
}
```

## Branch Side Effects

Use `OnOk` and `OnOkAsync` for side effects on the successful branch. Use `OnErr` and `OnErrAsync` for side effects on the failed branch. Each method returns the original result.

```csharp
return await LoadUserAsync(id)
    .OnOkAsync(user => AuditAsync($"loaded {user.Id}"))
    .OnErrAsync(error => LogAsync(error.Message));
```

## Consuming Results

Use `Match` when both branches should produce one value or run one matching side effect:

```csharp
var text = result.Match(
    value => $"ok: {value}",
    error => $"err: {error.Message}");
```

Use `MatchAsync` for asynchronous branch handlers:

```csharp
await result.MatchAsync(
    value => WriteOkAsync(value),
    error => WriteErrAsync(error.Message));
```

## v0.1.0 Scope

The current release intentionally keeps the model small: one result type, one fixed error type, direct branch properties, and Task-based workflow helpers.
