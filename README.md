# OpResult

English | [简体中文](https://github.com/RokyZevon/OpResult/blob/main/README.zh.md)

OpResult is a small .NET Result Pattern library for explicit `Ok` and `Err` business flows. It is Native AOT compatible.

It provides two first-class result containers:

- `OpResult` for operations that either succeed without a payload or fail with an `OpError`.
- `OpResult<T>` for operations that either succeed with a non-null payload or fail with an `OpError`.

`OpError` carries a public `Message` and optional `InnerError`. It is an error details object produced by `OpResults.Err(...)` and can be converted to a failed `OpResult` or `OpResult<T>`.

## Installation

```bash
dotnet add package RokyZevon.OpResult
```

## Quick Start

Return `OpResult<T>` from operations that can fail. For traditional .NET short-circuit flow, guard with `IsErr` or `IsOk`, then read `Error` or `Value` directly on the verified branch.

```csharp
public string GetUserDisplayName(Guid userId)
{
    OpResult<User> result = FindUser(userId);

    if (result.IsErr)
    {
        return $"Could not load user: {result.Error.Message}";
    }

    User user = result.Value;
    return $"Loaded {user.DisplayName}.";
}
```

For multi-step workflows, chain asynchronous result-producing steps with `ThenAsync`, then `await` the pipeline and use `Match` when the final branch mapping is synchronous.

```csharp
public async Task<string> GetUserDisplayNameAsync(Guid userId)
{
    OpResult<User> result = await ValidateUserIdAsync(userId)
        .ThenAsync(() => LoadUserAsync(userId))
        .ThenAsync(user => EnsureActiveAsync(user));

    return result.Match(
        onOk: user => $"Loaded {user.DisplayName}.",
        onErr: error => $"Could not load user: {error.Message}");
}

Task<OpResult> ValidateUserIdAsync(Guid userId) =>
    validationService.ValidateUserIdAsync(userId);

Task<OpResult<User>> LoadUserAsync(Guid userId) =>
    repository.LoadUserAsync(userId);

Task<OpResult<User>> EnsureActiveAsync(User user) =>
    userPolicy.EnsureActiveAsync(user);
```

## Usage

### Choosing the Right API

| Use this | When you need to |
| --- | --- |
| `IsOk` / `IsErr` + `Value` / `Error` | Use traditional .NET branching, early returns, short-circuit flow, or direct local access to the success value or error details. |
| `Then` / `ThenAsync` | Continue to another operation that can fail. `Err` short-circuits and the continuation is not called. |
| `Match` | Finish the workflow when both branch handlers are synchronous. |
| `MatchAsync` | Finish the workflow when either branch handler calls asynchronous work and returns `Task`. |
| `OnOk` / `OnErr` | Run logging, metrics, auditing, or other side effects without changing the result. |
| `TryInvoke` / `TryInvokeAsync` | Adapt exception-throwing boundary code into `OpResult` / `OpResult<T>`. |

### Creating Results

Use `OpResult` for command-style operations that do not produce a success value:

```csharp
OpResult SaveAuditLog(string text)
{
    if (string.IsNullOrWhiteSpace(text))
    {
        return OpResults.Err("Audit text is required.");
    }

    File.AppendAllText("audit.log", text);
    return OpResults.Ok();
}
```

Use `OpResult<T>` for query-style operations that produce a non-null success value:

```csharp
OpResult<User> FindUser(Guid id)
{
    User? user = repository.Find(id);

    return user is null
        ? OpResults.Err("User was not found.")
        : OpResults.Ok(user);
}
```

`OpResults.Err<T>(...)` remains available as an explicit compatibility form, but new code should prefer `OpResults.Err(...)` and let the target result type perform the conversion.

A non-null `T` can also be returned directly as a successful `OpResult<T>`:

```csharp
OpResult<int> CountActiveUsers()
{
    return repository.CountActiveUsers();
}
```

### Reading Value and Error After Guards

`Value` and `Error` are first-class APIs for direct branch handling. Check the result branch first, then read the matching property.

```csharp
string FormatUser(Guid userId)
{
    OpResult<User> result = FindUser(userId);

    if (result.IsErr)
    {
        logger.Warn(result.Error.Message);
        return "User unavailable.";
    }

    User user = result.Value;
    return user.DisplayName;
}
```

For `OpResult` without a success payload, read `Error` on the failed branch:

```csharp
void SaveAuditOrLog(string text)
{
    OpResult saved = SaveAuditLog(text);

    if (saved.IsErr)
    {
        logger.Warn(saved.Error.Message);
        return;
    }

    logger.Info("Audit log saved.");
}
```

### Wrapping Errors with InnerError

Use `InnerError` when an outer operation needs to preserve the error from a lower-level operation. After an `IsErr` guard, `result.Error.ToErr(...)` creates a new outer `OpError` and keeps the original error as `InnerError`.

```csharp
OpResult<UserProfile> LoadProfile(Guid userId)
{
    OpResult<User> userResult = LoadUser(userId);

    if (userResult.IsErr)
    {
        return userResult.Error.ToErr("Could not load profile.");
    }

    return profileRepository.Load(userResult.Value.Id);
}
```

If you already have the inner error and need to create an outer error directly, use `OpResults.Err(message, innerError)`:

```csharp
if (userResult.IsErr)
{
    return OpResults.Err("Could not load profile.", userResult.Error);
}
```

For one-line display or logging, `ToString()` returns the chain from outer error to inner error and skips empty messages:

```csharp
OpError error = OpResults.Err("User was not found.")
    .ToErr("Could not load profile.");

logger.LogError("{Error}", error);
```

Expected display:

```text
Could not load profile. -> User was not found.
```

If every message in the chain is empty, `ToString()` returns `"<error>"`. `ToString()` is for human-readable display, not a stable parsing protocol.

### Chaining Steps with Then and ThenAsync

Use `Then` when the next synchronous step can also fail. It runs only after an `Ok` result. If the current result is `Err`, the continuation is not called and the original `OpError` is carried forward.

```csharp
OpResult SuspendUser(Guid userId)
{
    return ValidateUserId(userId)
        .Then(() => WriteAuditEntry(userId))
        .Then(() => MarkUserSuspended(userId));
}

OpResult ValidateUserId(Guid userId) =>
    userId == Guid.Empty
        ? OpResults.Err("User id is required.")
        : OpResults.Ok();
```

`Then` supports the common void/value transitions:

| Current result | Continuation | Result |
| --- | --- | --- |
| `OpResult` | `Func<OpResult>` | `OpResult` |
| `OpResult` | `Func<OpResult<T>>` | `OpResult<T>` |
| `OpResult<T>` | `Func<T, OpResult<TNext>>` | `OpResult<TNext>` |
| `OpResult<T>` | `Func<T, OpResult>` | `OpResult` |

```csharp
OpResult<User> LoadValidUser(Guid userId)
{
    return ValidateUserId(userId)
        .Then(() => LoadUser(userId));
}

OpResult<string> LoadDisplayName(Guid userId)
{
    return LoadUser(userId)
        .Then(user => LoadProfile(user.Id))
        .Then(profile => OpResults.Ok(profile.DisplayName));
}

OpResult SendWelcomeEmail(Guid userId)
{
    return LoadUser(userId)
        .Then(user => emailSender.SendWelcome(user.Email));
}
```

Use `ThenAsync` when the next step returns `Task<OpResult>` or `Task<OpResult<T>>`. You can call it on a direct result or on `Task<OpResult*>` to keep an async pipeline chained.

```csharp
public Task<OpResult<string>> LoadDisplayNameAsync(Guid userId)
{
    return ValidateUserIdAsync(userId)
        .ThenAsync(() => LoadUserAsync(userId))
        .ThenAsync(user => LoadProfileAsync(user.Id))
        .ThenAsync(profile => LoadDisplayNameResultAsync(profile));
}
```

The first `ThenAsync` above receives a `Task<OpResult>`. The later calls receive `Task<OpResult<T>>`, so each continuation gets the successful value from the previous step. Every `ThenAsync` continuation returns `Task<OpResult>` or `Task<OpResult<T>>`, including the value-returning `LoadDisplayNameResultAsync` step:

```csharp
Task<OpResult> ValidateUserIdAsync(Guid userId) =>
    validationService.ValidateUserIdAsync(userId);

Task<OpResult<string>> LoadDisplayNameResultAsync(UserProfile profile) =>
    profileStore.LoadDisplayNameResultAsync(profile);
```

### Consuming Results with Match and MatchAsync

Use `Match` when the workflow is finished and both branches must be handled. Unlike `Then`, `Match` does not continue the business pipeline. It converts the result into a final value or consumes both branches through actions.

Fold a value result into another value:

```csharp
string response = FindUser(userId).Match(
    onOk: user => $"Loaded {user.DisplayName}.",
    onErr: error => $"Could not load user: {error.Message}");
```

Fold a result without a success payload:

```csharp
string status = SaveAuditLog(text).Match(
    onOk: () => "Audit log saved.",
    onErr: error => $"Audit log failed: {error.Message}");
```

Consume both branches through actions:

```csharp
FindUser(userId).Match(
    onOk: user => logger.Info($"Loaded {user.Id}."),
    onErr: error => logger.Warn(error.Message));
```

If the result comes from an asynchronous pipeline but both branch handlers are synchronous, first `await` the pipeline and then call `Match`:

```csharp
OpResult<User> result = await ValidateUserIdAsync(userId)
    .ThenAsync(() => LoadUserAsync(userId))
    .ThenAsync(user => EnsureActiveAsync(user));

string message = result.Match(
    onOk: user => $"Loaded {user.DisplayName}.",
    onErr: error => $"Could not load user: {error.Message}");
```

Use `MatchAsync` when branch handlers are asynchronous and return `Task<TResult>` or `Task`.

```csharp
string message = await ValidateUserIdAsync(userId)
    .ThenAsync(() => LoadUserAsync(userId))
    .ThenAsync(user => EnsureActiveAsync(user))
    .MatchAsync(
        onOk: user => FormatLoadedUserAsync(user),
        onErr: error => FormatLoadErrorAsync(error));
```

```csharp
Task<string> FormatLoadedUserAsync(User user) =>
    localization.FormatAsync("user.loaded", user.DisplayName);

Task<string> FormatLoadErrorAsync(OpError error) =>
    localization.FormatAsync("user.failed", error.Message);
```

`MatchAsync` can also consume asynchronous branch actions:

```csharp
await LoadUserAsync(userId).MatchAsync(
    onOk: user => WriteLoadedAuditAsync(user),
    onErr: error => WriteFailedAuditAsync(error));
```

```csharp
Task WriteLoadedAuditAsync(User user) =>
    audit.WriteAsync($"Loaded {user.Id}.");

Task WriteFailedAuditAsync(OpError error) =>
    audit.WriteAsync($"Failed: {error.Message}");
```

### Running Side Effects with OnOk and OnErr

Use `OnOk` and `OnErr` for logging, metrics, auditing, and other side effects that should return the original result.

```csharp
OpResult<User> loaded = LoadUser(userId)
    .OnOk(user => metrics.Increment("user.loaded"))
    .OnErr(error => logger.Warn(error.Message));
```

Asynchronous side effects use `OnOkAsync` and `OnErrAsync`. They also work directly on `Task<OpResult>` and `Task<OpResult<T>>`, so an async workflow can stay chained:

```csharp
OpResult<User> loaded = await LoadUserAsync(userId)
    .OnOkAsync(user => audit.WriteAsync($"Loaded {user.Id}."))
    .OnErrAsync(error => audit.WriteAsync($"Load failed: {error.Message}"));
```

### Wrapping Exception Boundaries with TryInvoke

Use `TryInvoke` and `TryInvokeAsync` at boundaries where exceptions should become `Err` results.

```csharp
OpResult written = OpResults.TryInvoke(
    () => File.WriteAllText(path, text));

OpResult<User> loaded = OpResults.TryInvoke(
    () => legacyRepository.LoadUser(userId));

OpResult saved = await OpResults.TryInvokeAsync(
    () => repository.SaveAsync(user, cancellationToken));

OpResult<User> fetched = await OpResults.TryInvokeAsync(
    () => repository.LoadUserAsync(userId, cancellationToken));
```

`TryInvoke` uses zero-argument delegates. Pass arguments and cancellation tokens through lambdas or closures.

The boundary rules are:

- A null delegate throws `ArgumentNullException`.
- A non-cancellation exception becomes an `Err` whose message starts with the full exception type name.
- Inner exceptions are preserved as an `OpError.InnerError` chain.
- A null returned task or null returned payload becomes `Err("Operation returned null.")`.
- `OperationCanceledException` and derived cancellation exceptions propagate.

For example, an `InvalidOperationException("outer failed", new ArgumentException("bad user id"))` displays as:

```text
System.InvalidOperationException: outer failed -> System.ArgumentException: bad user id
```

## Result Boundaries

Successful payloads are non-null by design. `OpResult<User?>` and `OpResults.Ok<User?>(null)` are outside the supported success model.

`default(OpResult)` and `default(OpResult<T>)` are `Err` results with an empty error message.

Null, empty, and whitespace error messages are normalized to `string.Empty`:

```csharp
OpResult result = OpResults.Err("   ");

Console.WriteLine(result.IsErr);          // True
Console.WriteLine(result.Error!.Message); // ""
```

Wrong-branch property reads are runtime fallbacks, not control-flow APIs:

- Reading `Value` on an `Err` result returns `default(T)`.
- Reading `Error` on an `Ok` result returns an empty-message `OpError`.

Do not use `Value != null`, `Error != null`, or `Error.Message == string.Empty` to decide whether a result is `Ok` or `Err`. Use `IsOk`, `IsErr`, `Then`, or `Match` instead.
