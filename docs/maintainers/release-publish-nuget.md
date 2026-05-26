# Release and GitHub Packages (NuGet)

This repository includes `.github/workflows/publish-nuget.yml` and follows GitHub official guidance for Releases and the NuGet registry on GitHub Packages:

- Trigger: when a GitHub Release is published (`release.published`), or manually by `workflow_dispatch`.
- Auth: publish with `GITHUB_TOKEN` (no hardcoded PAT in repository files).
- Permissions: workflow uses `packages: write`.
- Source: `https://nuget.pkg.github.com/RokyZevon/index.json`.

## v0.1.0 release steps

1. Ensure the release tag is `v0.1.0`.
2. In GitHub UI, open **Releases** and create/publish release `v0.1.0` from the desired commit/branch.
3. After release is published, the workflow packs and pushes `OpResult.0.1.0.nupkg` to GitHub Packages automatically.

## When manual intervention may be required

- If package publish fails due to permissions, confirm repository **Actions** has permission to create and publish packages, and `GITHUB_TOKEN` has package write access.
- If your org/account restricts package visibility or workflow access inheritance, adjust package access settings in GitHub Packages so this repository workflow can publish/read as needed.
