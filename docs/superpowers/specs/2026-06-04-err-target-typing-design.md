# Err Target Typing Design

OpResult v0.1.1 changes `OpResults.Err(string?)` back to an `OpError` factory so failed results can be returned without spelling generic type arguments.

```csharp
OpResult Save() => OpResults.Err("failed");

OpResult<User> GetUser() => OpResults.Err("not found");
```

`OpResult` and `OpResult<T>` declare implicit conversions from `OpError`. The conversions throw `ArgumentNullException` when the `OpError` instance is null.

```csharp
public static implicit operator OpResult(OpError error);

public static implicit operator OpResult<T>(OpError error)
    where T : notnull;
```

The conversions live on the target result types. This is required for the generic `OpResult<T>` conversion and keeps the design symmetric with the existing `T -> OpResult<T>` success conversion.

`OpResults.Err<T>(string?)` remains available in v0.1.1 for source compatibility and is not obsolete. New examples should prefer `OpResults.Err(string?)` and let the target result type perform the conversion.

`OpResults.Err(null)` and whitespace messages continue to normalize to an empty-message `OpError`. This is separate from null `OpError` conversion: a null error object is invalid and throws at the conversion boundary.

This spec supersedes only the Err factory and OpError conversion parts of the v0.1.0 core API baseline in `2026-05-17-opresult-api-design.md`.
