# Release 与 GitHub Packages（NuGet）发布

仓库已包含 `.github/workflows/publish-nuget.yml`，并按 GitHub 官方关于 Releases 与 GitHub Packages NuGet registry 的方式配置：

- 触发方式：发布 GitHub Release（`release.published`）或手动 `workflow_dispatch`。
- 鉴权方式：使用 `GITHUB_TOKEN`，仓库内不硬编码 PAT。
- 权限要求：工作流申请 `packages: write`。
- 推送方式：`dotnet nuget push` 直接推到 `https://nuget.pkg.github.com/<OWNER>/index.json`（不使用 `nuget add source` 持久化凭据）。

## v0.1.0 发布步骤

1. 确保 release tag 为 `v0.1.0`。
2. 在 GitHub 仓库页面进入 **Releases**，基于目标提交/分支创建并发布 `v0.1.0`。
3. Release 发布后，工作流会自动打包并推送 `OpResult.0.1.0.nupkg` 到 GitHub Packages。

## 可能需要人工介入的场景

- 若发布因权限失败，请确认仓库 **Actions** 具备创建/发布 packages 的权限，且 `GITHUB_TOKEN` 对 package 具备写权限。
- 在仓库 **Settings → Actions → General → Workflow permissions** 中，确保启用 **Read and write permissions**（使工作流内声明的 `packages: write` 权限生效）。
- 若组织/账号关闭了 package 权限继承或限制了 workflow 访问，需要在 GitHub Packages 的包权限设置中手动放通该仓库工作流访问。
