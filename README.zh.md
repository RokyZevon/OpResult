# OpResult

[English](https://github.com/RokyZevon/OpResult/blob/main/README.md) | 简体中文

OpResult 是一个轻量的 .NET Result Pattern 类库，用显式 `Ok` 和 `Err` 路径表达业务流程。

它提供两个一等结果容器：

- `OpResult`：用于成功时不携带载荷、失败时携带 `OpError` 的操作。
- `OpResult<T>`：用于成功时携带 non-null 载荷、失败时携带 `OpError` 的操作。

`OpError` 暴露的 public surface 是 `Message` 属性。它是错误详情对象，不是结果载体：`OpResults.Err(...)` 返回 result，不返回 `OpError`，`OpError` 也不会隐式转换为 `OpResult` 或 `OpResult<T>`。

## 安装

```bash
dotnet add package RokyZevon.OpResult
```

## 快速开始

让可能失败的操作返回 `OpResult<T>`，用 `ThenAsync` 串联异步且会产生 result 的步骤；如果最终分支映射是同步逻辑，先 `await` pipeline，再使用 `Match`。

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

## 用法

### 如何选择 API

| 使用 | 适合场景 |
| --- | --- |
| `Then` / `ThenAsync` | 继续执行另一个可能失败的操作。遇到 `Err` 会短路，不调用后续 continuation。 |
| `Match` | 当两个分支 handler 都是同步逻辑时，结束工作流。 |
| `MatchAsync` | 当任一分支 handler 调用异步工作并返回 `Task` 时，结束工作流。 |
| `OnOk` / `OnErr` | 执行日志、指标、审计等副作用，不改变 result。 |
| `TryInvoke` / `TryInvokeAsync` | 把会抛异常的边界代码适配成 `OpResult` / `OpResult<T>`。 |

### 创建结果

命令型操作不产生成功值，使用 `OpResult`：

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

查询型操作会产生 non-null 成功值，使用 `OpResult<T>`：

```csharp
OpResult<User> FindUser(Guid id)
{
    User? user = repository.Find(id);

    return user is null
        ? OpResults.Err<User>("User was not found.")
        : OpResults.Ok(user);
}
```

non-null 的 `T` 也可以直接作为成功的 `OpResult<T>` 返回：

```csharp
OpResult<int> CountActiveUsers()
{
    return repository.CountActiveUsers();
}
```

### 使用 Then 和 ThenAsync 串联步骤

当下一个同步步骤也可能失败时，使用 `Then`。它只会在当前结果为 `Ok` 时运行；如果当前结果是 `Err`，continuation 不会被调用，原错误消息会继续向后传递。

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

`Then` 支持常见的 void/value 转换：

| 当前结果 | Continuation | 返回结果 |
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

当下一步返回 `Task<OpResult>` 或 `Task<OpResult<T>>` 时，使用 `ThenAsync`。它既可以接在直接 result 后面，也可以接在 `Task<OpResult*>` 后面，让异步 pipeline 保持链式写法。

```csharp
public Task<OpResult<string>> LoadDisplayNameAsync(Guid userId)
{
    return ValidateUserIdAsync(userId)
        .ThenAsync(() => LoadUserAsync(userId))
        .ThenAsync(user => LoadProfileAsync(user.Id))
        .ThenAsync(profile => LoadDisplayNameResultAsync(profile));
}
```

上面第一个 `ThenAsync` 接收的是 `Task<OpResult>`。后续调用接收的是 `Task<OpResult<T>>`，因此每个 continuation 都能拿到上一步的成功值。每个 `ThenAsync` continuation 都返回 `Task<OpResult>` 或 `Task<OpResult<T>>`，包括返回成功值的 `LoadDisplayNameResultAsync` 步骤：

```csharp
Task<OpResult> ValidateUserIdAsync(Guid userId) =>
    validationService.ValidateUserIdAsync(userId);

Task<OpResult<string>> LoadDisplayNameResultAsync(UserProfile profile) =>
    profileStore.LoadDisplayNameResultAsync(profile);
```

### 使用 Match 和 MatchAsync 消费结果

当工作流已经结束、两个分支都必须处理时，使用 `Match`。和 `Then` 不同，`Match` 不继续业务 pipeline，而是把 result 转成最终值，或通过 action 消费两个分支。

把有成功值的 result 折叠成另一个值：

```csharp
string response = FindUser(userId).Match(
    onOk: user => $"Loaded {user.DisplayName}.",
    onErr: error => $"Could not load user: {error.Message}");
```

把没有成功载荷的 result 折叠成另一个值：

```csharp
string status = SaveAuditLog(text).Match(
    onOk: () => "Audit log saved.",
    onErr: error => $"Audit log failed: {error.Message}");
```

通过 action 消费两个分支：

```csharp
FindUser(userId).Match(
    onOk: user => logger.Info($"Loaded {user.Id}."),
    onErr: error => logger.Warn(error.Message));
```

如果 result 来自异步 pipeline，但两个分支 handler 都是同步逻辑，先 `await` pipeline 得到 result，再调用 `Match`：

```csharp
OpResult<User> result = await ValidateUserIdAsync(userId)
    .ThenAsync(() => LoadUserAsync(userId))
    .ThenAsync(user => EnsureActiveAsync(user));

string message = result.Match(
    onOk: user => $"Loaded {user.DisplayName}.",
    onErr: error => $"Could not load user: {error.Message}");
```

当分支 handler 是异步的，并返回 `Task<TResult>` 或 `Task` 时，使用 `MatchAsync`。

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

`MatchAsync` 也可以通过异步 action 消费两个分支：

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

### 使用 OnOk 和 OnErr 执行副作用

日志、指标、审计等不应改变原结果的副作用，可以使用 `OnOk` 和 `OnErr`：

```csharp
OpResult<User> loaded = LoadUser(userId)
    .OnOk(user => metrics.Increment("user.loaded"))
    .OnErr(error => logger.Warn(error.Message));
```

异步副作用使用 `OnOkAsync` 和 `OnErrAsync`。它们也可以直接接在 `Task<OpResult>` 和 `Task<OpResult<T>>` 后面，让异步流程保持链式写法：

```csharp
OpResult<User> loaded = await LoadUserAsync(userId)
    .OnOkAsync(user => audit.WriteAsync($"Loaded {user.Id}."))
    .OnErrAsync(error => audit.WriteAsync($"Load failed: {error.Message}"));
```

### 使用 TryInvoke 包装异常边界

在需要把异常边界折叠成 `Err` 结果的位置，使用 `TryInvoke` 和 `TryInvokeAsync`：

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

`TryInvoke` 使用零参数委托。需要传入参数或 cancellation token 时，通过 lambda 或闭包传给实际业务方法。

边界规则如下：

- null delegate 会抛出 `ArgumentNullException`。
- 非取消异常会转成 `Err(exception.Message)`。
- 返回 null task 或 null payload 会转成 `Err("Operation returned null.")`。
- `OperationCanceledException` 及其派生的取消异常会继续传播。

## 结果边界

成功载荷按 non-null 设计。`OpResult<User?>` 和 `OpResults.Ok<User?>(null)` 不属于受支持的成功模型。

`default(OpResult)` 和 `default(OpResult<T>)` 都是空错误消息的 `Err` 结果。

null、空字符串和空白错误消息都会归一化为 `string.Empty`：

```csharp
OpResult result = OpResults.Err("   ");

Console.WriteLine(result.IsErr);          // True
Console.WriteLine(result.Error!.Message); // ""
```

错分支属性读取是运行时兜底，不是控制流 API：

- 在 `Err` 结果上读取 `Value` 会返回 `default(T)`。
- 在 `Ok` 结果上读取 `Error` 会返回空消息 `OpError`。

不要用 `Value != null`、`Error != null` 或 `Error.Message == string.Empty` 判断结果是 `Ok` 还是 `Err`。请使用 `IsOk`、`IsErr`、`Then` 或 `Match`。
