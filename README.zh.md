# OpResult

OpResult 是一个轻量的 .NET Result Pattern 类库，用显式 `Ok`/`Err` 路径表达业务流程。

## 核心类型

v0.1.0 将两种结果容器都作为一等类型：

- `OpResult`：用于没有成功载荷的操作。
- `OpResult<T>`：用于有成功载荷的操作，且成功值要求 non-null（`where T : notnull`）。
- `OpError`：错误信息对象（`Message`），不是结果载体。

## 工厂方法

```csharp
OpResult ok = OpResults.Ok();
OpResult<int> okValue = OpResults.Ok(42);

OpResult err = OpResults.Err("write failed");
OpResult<int> errValue = OpResults.Err<int>("count failed");
```

工厂返回形态：

```csharp
OpResults.Ok()            // OpResult
OpResults.Ok<T>(value)    // OpResult<T>
OpResults.Err(message)    // OpResult
OpResults.Err<T>(message) // OpResult<T>
```

`OpResults.Err(...)` 不返回 `OpError`，`OpError` 也不提供到 `OpResult` 或 `OpResult<T>` 的隐式转换。

## TryInvoke

当异常边界代码需要折叠成 `OpResult` 时，使用 `TryInvoke`：

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

非取消异常会转成 `Err(exception.Message)`。取消异常会继续传播。若被适配的有值操作返回 `null`，或异步操作返回 null task，结果为 `Err("Operation returned null.")`。

需要传入参数或 cancellation token 时，使用 lambda / 闭包传给实际业务方法。

## 分支消费

推荐使用 `IsOk` / `IsErr` 做分支判断，并用 `Then` / `Match` 表达流程与最终消费。

```csharp
OpResult result = BeginTransaction()
    .Then(WriteAuditLog)
    .Then(CommitTransaction);

string text = result.Match(
    onOk: () => "committed",
    onErr: error => $"failed: {error.Message}");
```

`Value` / `Error` 在错分支访问时有运行时兜底：

- 在 `Err` 分支读取 `Value` 返回 `default(T)`。
- 在 `Ok` 分支读取 `Error` 返回空消息 `OpError`。

这些兜底是运行时保护，不是推荐的业务分支判断方式。
不要用 `Value != null`、`Error != null` 或 `Error.Message == string.Empty` 判断结果到底是 `Ok` 还是 `Err`。

## 异步 service-layer 示例

命令型操作使用 `Task<OpResult>`：

```csharp
public async Task<OpResult> SuspendUserAsync(Guid userId)
{
    return await LoadUserAsync(userId)
        .ThenAsync(user => EnsureCanSuspendAsync(user))
        .ThenAsync(() => MarkSuspendedAsync(userId))
        .OnErrAsync(error => AuditAsync($"suspend failed: {error.Message}"));
}
```

查询型操作使用 `Task<OpResult<T>>`：

```csharp
public async Task<OpResult<User>> GetUserAsync(Guid userId)
{
    return await ValidateUserIdAsync(userId)
        .ThenAsync(() => _repository.GetUserAsync(userId))
        .OnOkAsync(user => AuditAsync($"loaded: {user.Id}"));
}
```

## v0.1.0 边界

- 成功载荷按 non-null 设计，不支持把 `OpResult<User?>` 或 `OpResults.Ok<User?>(null)` 作为成功模型。
- `TryInvoke` 覆盖 `Action`、`Func<T>`、`Func<Task>` 与 `Func<Task<T>>` 的异常边界适配。

## Release 与 GitHub Packages（NuGet）发布

仓库已包含 `.github/workflows/publish-nuget.yml`，并按 GitHub 官方关于 Releases 与 GitHub Packages NuGet registry 的方式配置：

- 触发方式：发布 GitHub Release（`release.published`）或手动 `workflow_dispatch`。
- 鉴权方式：使用 `GITHUB_TOKEN`，仓库内不硬编码 PAT。
- 权限要求：工作流申请 `packages: write`。
- 发布源：`https://nuget.pkg.github.com/RokyZevon/index.json`。

### v0.1.0 发布步骤

1. 确保 release tag 为 `v0.1.0`。
2. 在 GitHub 仓库页面进入 **Releases**，基于目标提交/分支创建并发布 `v0.1.0`。
3. Release 发布后，工作流会自动打包并推送 `OpResult.0.1.0.nupkg` 到 GitHub Packages。

### 可能需要人工介入的场景

- 若发布因权限失败，请确认仓库 **Actions** 具备创建/发布 packages 的权限，且 `GITHUB_TOKEN` 对 package 具备写权限。
- 若组织/账号关闭了 package 权限继承或限制了 workflow 访问，需要在 GitHub Packages 的包权限设置中手动放通该仓库工作流访问。
