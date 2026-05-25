# OpResult

OpResult is a small .NET Result Pattern library for explicit `Ok`/`Err` business flows.

## Core Types

v0.1.0 treats both result containers as first-class types:

- `OpResult`: for operations without a success payload.
- `OpResult<T>`: for operations with a non-null success payload (`where T : notnull`).
- `OpError`: an error details object (`Message`), not a result carrier.

## Factories

```csharp
OpResult ok = OpResults.Ok();
OpResult<int> okValue = OpResults.Ok(42);

OpResult err = OpResults.Err("write failed");
OpResult<int> errValue = OpResults.Err<int>("count failed");
```

Factory shape:

```csharp
OpResults.Ok()            // OpResult
OpResults.Ok<T>(value)    // OpResult<T>
OpResults.Err(message)    // OpResult
OpResults.Err<T>(message) // OpResult<T>
```

`OpResults.Err(...)` does not return `OpError`, and `OpError` does not implicitly convert to `OpResult` or `OpResult<T>`.

## TryInvoke

Use `TryInvoke` when code at an exception boundary should be folded into `OpResult`:

```csharp
OpResult written = OpResults.TryInvoke(
    () => File.WriteAllText(path, text));

OpResult<User> loaded = OpResults.TryInvoke(
    () => repository.LoadUser(id));

OpResult saved = await OpResults.TryInvokeAsync(
    () => repository.SaveAsync(user, cancellationToken));

OpResult<User> fetched = await OpResults.TryInvokeAsync(
    () => repository.LoadUserAsync(id, cancellationToken));
```

Non-cancellation exceptions become `Err(exception.Message)`. Cancellation exceptions propagate. If an adapted value-returning operation returns `null`, or an async operation returns a `null` task, the result is `Err("Operation returned null.")`.

Use lambdas or closures to pass arguments or cancellation tokens to the adapted operation.

## Consuming Branches

Use `IsOk` / `IsErr` for branch checks, and use `Then` / `Match` to express workflow and final consumption.

```csharp
OpResult result = BeginTransaction()
    .Then(WriteAuditLog)
    .Then(CommitTransaction);

string text = result.Match(
    onOk: () => "committed",
    onErr: error => $"failed: {error.Message}");
```

`Value` and `Error` have runtime fallback behavior:

- Reading `Value` on an `Err` result returns `default(T)`.
- Reading `Error` on an `Ok` result returns an empty-message `OpError`.

These fallbacks are runtime guards, not the recommended way to drive control flow.
Do not use `Value != null`, `Error != null`, or `Error.Message == string.Empty` to decide whether a result is `Ok` or `Err`.

## Async Service-Layer Examples

Use `Task<OpResult>` for command-style operations:

```csharp
public async Task<OpResult> SuspendUserAsync(Guid userId)
{
    return await LoadUserAsync(userId)
        .ThenAsync(user => EnsureCanSuspendAsync(user))
        .ThenAsync(() => MarkSuspendedAsync(userId))
        .OnErrAsync(error => AuditAsync($"suspend failed: {error.Message}"));
}
```

Use `Task<OpResult<T>>` for query-style operations:

```csharp
public async Task<OpResult<User>> GetUserAsync(Guid userId)
{
    return await ValidateUserIdAsync(userId)
        .ThenAsync(() => _repository.GetUserAsync(userId))
        .OnOkAsync(user => AuditAsync($"loaded: {user.Id}"));
}
```

## v0.1.0 Boundaries

- Successful payloads are non-null by design. `OpResult<User?>` and `OpResults.Ok<User?>(null)` are out of scope.
- `TryInvoke` covers exception-boundary adapters for `Action`, `Func<T>`, `Func<Task>`, and `Func<Task<T>>`.

## Release and GitHub Packages (NuGet)

This repository includes `.github/workflows/publish-nuget.yml` and follows GitHub official guidance for Releases and the NuGet registry on GitHub Packages:

- Trigger: when a GitHub Release is published (`release.published`), or manually by `workflow_dispatch`.
- Auth: publish with `GITHUB_TOKEN` (no hardcoded PAT in repository files).
- Permissions: workflow uses `packages: write`.
- Source: `https://nuget.pkg.github.com/RokyZevon/index.json`.

### v0.1.0 release steps

1. Ensure the release tag is `v0.1.0`.
2. In GitHub UI, open **Releases** and create/publish release `v0.1.0` from the desired commit/branch.
3. After release is published, the workflow packs and pushes `OpResult.0.1.0.nupkg` to GitHub Packages automatically.

### When manual intervention may be required

- If package publish fails due to permissions, confirm repository **Actions** has permission to create and publish packages, and `GITHUB_TOKEN` has package write access.
- If your org/account restricts package visibility or workflow access inheritance, adjust package access settings in GitHub Packages so this repository workflow can publish/read as needed.
