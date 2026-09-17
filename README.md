# Workflows

[![GitHub last commit (branch)](https://img.shields.io/github/last-commit/bigbang1112/workflows/main?style=for-the-badge&logo=github)](https://github.com/BigBang1112/workflows)

A collection of reusable GitHub Actions workflows for .NET development. Reference them from any repository using `workflow_call`.

## Usage

```yaml
jobs:
  ci:
    uses: bigbang1112/workflows/.github/workflows/ci.yml@main
    with:
      dotnet-version: 10.x.x
```

Secrets are forwarded explicitly:

```yaml
    secrets:
      NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

## Workflows

### `ci.yml` — CI Build & Test

Builds and tests a .NET solution across one or more OS runners with optional code coverage.

| Input | Description | Default |
|---|---|---|
| `dotnet-version` | .NET version | `latest` |
| `workloads` | Comma-separated workloads to install | |
| `os` | JSON array of runners | `["ubuntu-latest", "windows-latest"]` |
| `build-args` | Extra `dotnet build` arguments | |
| `test-args` | Extra `dotnet test` arguments | |
| `enable-coverage` | Generate and publish coverage summary | `true` |
| `enable-cache` | Cache NuGet packages | `true` |

### `deploy-pages.yml` — Deploy Blazor WASM to GitHub Pages

Publishes a Blazor WebAssembly project and deploys it to GitHub Pages. By default, patches `<base href>` and `service-worker.published.js` to the repository name.

Requires `pages: write` and `id-token: write` permissions on the caller.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the Blazor WASM project | |
| `dotnet-version` | .NET version | `latest` |
| `workloads` | Comma-separated workloads to install | `wasm-tools` |
| `enable-cache` | Cache NuGet packages | `true` |
| `enable-coverage` | Generate HTML report at `/coverage/` on the site | `false` |
| `patch-base` | Patch `<base href>` in `index.html` and `service-worker.published.js` to the repository name | `true` |

### `docker-publish.yml` — Publish Docker Images

Builds and pushes Docker images to Docker Hub and/or GitHub Container Registry using a matrix of projects.

| Input | Description | Default |
|---|---|---|
| `matrix` | *(required)* JSON array of `{project, image_name}` objects | |
| `platforms` | Target platforms for Docker build | `linux/amd64,linux/arm64` |
| `push-to-dockerhub` | Push images to Docker Hub | `true` |
| `push-to-ghcr` | Push images to GitHub Container Registry | `true` |
| `dockerhub-username` | Docker Hub username (defaults to GitHub repository owner if not provided) | `""` |

| Secret | Description |
|---|---|
| `DOCKERHUB_TOKEN` | Docker Hub access token |

### `publish-nuget.yml` — Publish NuGet Packages

Builds, tests, packs, and publishes NuGet packages to nuget.org and/or GitHub Packages.

During packing, each project's `PackageReleaseNotes` is set from its `[project-folder]/Changelogs/v[Version].md` file, if present, where `Version` is the project's `Version` property, or `VersionPrefix` combined with `VersionSuffix` (e.g. `1.2.3-beta.1`) if set.

| Input | Description | Default |
|---|---|---|
| `package-prefix` | Filter packages by prefix (e.g. `MyCompany.`) | |
| `project-path` | Path(s) to build and pack. Supports wildcards. | |
| `test-path` | Path(s) to test projects. Supports wildcards. | |
| `pack-path` | Path(s) to pack. Supports wildcards. Defaults to `project-path`. | |
| `dotnet-version` | .NET version | `latest` |
| `workloads` | Comma-separated workloads to install | |
| `enable-tests` | Run tests | `true` |
| `enable-coverage` | Generate and publish coverage summary | `true` |
| `push-to-nuget` | Publish to NuGet.org | `true` |
| `push-to-github` | Publish to GitHub Packages | `true` |
| `push-to-custom-feeds` | Publish to custom NuGet feeds (requires `custom-feed-urls` and `CUSTOM_FEED_API_KEYS`) | `false` |
| `custom-feed-urls` | Newline-separated list of custom NuGet feed source URLs | |
| `upload-to-release` | Upload `.nupkg` files to the GitHub Release | `true` |
| `nuget-username` | NuGet.org username for [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC), used to obtain a short-lived API key when `NUGET_API_KEY` is not provided | |
| `discord-top-lines` | Newline-separated lines inserted below the heading, before the changelog, in the Discord message | |
| `discord-bottom-lines` | Newline-separated lines appended at the very end of the Discord message | |

| Secret | Description |
|---|---|
| `NUGET_API_KEY` | nuget.org API key. Optional if `nuget-username` is set to use Trusted Publishing (OIDC) instead |
| `CUSTOM_FEED_API_KEYS` | Newline-separated API keys matching the order of `custom-feed-urls` |
| `DISCORD_WEBHOOK_URL` | Discord webhook URL. When set and at least one package was newly pushed, the release's notes are posted to it (heading + optional top lines + release body + GitHub/NuGet links + optional bottom lines), split to respect Discord's per-message character limit without breaking mid-line |

Requires `id-token: write` permission on the caller when using Trusted Publishing (`nuget-username`).

### `publish-nuget-immutable.yml` — Publish NuGet Packages (Immutable Release)

Builds, tests, packs, and publishes one or more .NET packages to NuGet.org, GitHub Packages, and/or custom feeds. Its helper validates each packed `.nupkg`'s actual ID and version against the matching project, then uses the project's `<PackageReleaseNotes>` MSBuild XML property for release notes. The job summary shows the result for every selected feed and symbols upload.

Whenever at least one package is uploaded for the first time, the workflow creates one GitHub Release for those newly uploaded packages—a pattern compatible with immutable releases. It attaches their `.nupkg` and `.snupkg` assets unless `upload-to-release` is disabled. When `main-project` is set, that package's release notes are first and unheaded; the other packages follow under a `## [Package ID] [Version]` heading. When it is empty, the highest newly uploaded version determines the release tag and every package's release notes have a `## [Package ID] [Version]` heading. The release tag uses the caller's tag ref when available, otherwise `v[main package version]`.

Release notes come directly from each project's `<PackageReleaseNotes>` MSBuild XML property. If `DISCORD_WEBHOOK_URL` is set, a release announcement using the same notes is sent after the release is created. It is split on line boundaries using Discord's UTF-16 character limit and retries rate-limited requests. The workflow runs the helper's unit tests before building; they are also available at `.github/scripts/test_publish_nuget_immutable.py`.

| Input | Description | Default |
|---|---|---|
| `package-prefix` | Filter packages by prefix (e.g. `MyCompany.`) | |
| `project-path` | Newline-separated project glob(s) to build. | |
| `test-path` | Newline-separated test-project glob(s). | |
| `pack-path` | Newline-separated project glob(s) to pack. Defaults to `project-path`, then the repository root. | |
| `dotnet-version` | .NET version | `latest` |
| `workloads` | Comma-separated workloads to install | |
| `enable-tests` | Run tests | `true` |
| `enable-coverage` | Generate and publish coverage summary when tests run | `true` |
| `push-to-nuget` | Publish to NuGet.org | `true` |
| `push-to-github` | Publish to GitHub Packages | `true` |
| `push-to-custom-feeds` | Publish to custom NuGet feeds (requires `custom-feed-urls` and `CUSTOM_FEED_API_KEYS`) | `false` |
| `custom-feed-urls` | Newline-separated list of custom NuGet feed source URLs | |
| `upload-to-release` | Attach newly uploaded `.nupkg` and `.snupkg` files to the GitHub Release | `true` |
| `main-project` | Package ID used for the release version and unheaded first release-notes section; when empty, the highest version determines the release version and all sections have headers | |
| `create-release` | Create one immutable GitHub Release for packages newly uploaded during this run | `true` |
| `discord-top-lines` | Newline-separated lines inserted below the heading, before the release notes, in the Discord message | |
| `discord-bottom-lines` | Newline-separated lines appended at the very end of the Discord message | |
| `release-title-prefix` | Optional text before the GitHub Release title version; one space is inserted between them. | `""` |

| Secret | Description |
|---|---|
| `NUGET_API_KEY` | NuGet.org API key. |
| `CUSTOM_FEED_API_KEYS` | Newline-separated API keys matching the order of `custom-feed-urls` |
| `DISCORD_WEBHOOK_URL` | Discord webhook URL. When set, the release notes are posted after the release is created, split on line boundaries to respect Discord's per-message character limit |

### `publish-artifact.yml` — Publish Artifacts

Publishes a .NET project for multiple runtimes, zips each output separately, computes a SHA256 checksum per zip (written to the job summary), and uploads all zips to the GitHub Release.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the project to publish (e.g. src/MyApp) | |
| `artifact-name` | *(required)* Base name for the generated zip files (e.g. MyApp) | |
| `artifact-root` | Directory inside the zip to place the published output under (e.g. MyApp). Leave empty for root. | `''` |
| `artifact-ignore` | Newline-separated list of file patterns to exclude from the zip (e.g. `*.pdb`). Patterns containing '/' are matched against the relative path, others against the file name. | `''` |
| `build-matrix` | JSON array of `{os, runtime}` objects | `[{"os":"windows-latest","runtime":"win-x64"},{"os":"ubuntu-latest","runtime":"linux-x64"}]` |
| `dotnet-version` | .NET version to use | `latest` |

### `publish-artifact-immutable.yml` — Publish Artifacts (Immutable Release)

Publishes a .NET project for multiple runtimes and creates a GitHub Release. This workflow zips each runtime output separately and attaches all of them to a single release. It will also post a Discord notification.

The release tag/version is read from the project's `Version` property, or `VersionPrefix` combined with `VersionSuffix` if set.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the project file (e.g. src/MyApp/MyApp.csproj) to extract version and build from. | |
| `artifact-name` | *(required)* Base name for the generated zip files (e.g. MyApp). | |
| `artifact-root` | Directory inside the zip to place the published output under (e.g. MyApp). Leave empty for root. | `''` |
| `artifact-ignore` | Newline-separated list of file patterns to exclude from the zip (e.g. `*.pdb`). | `''` |
| `build-matrix` | JSON array of `{os, runtime}` objects. | `[{"os":"windows-latest","runtime":"win-x64"},{"os":"ubuntu-latest","runtime":"linux-x64"}]` |
| `dotnet-version` | .NET version to use. | `latest` |
| `publish-release` | Create a GitHub Release (with tag) from the newly built ZIPs. | `true` |
| `discord-description` | Newline-separated lines inserted below the heading, before the changelog, in the Discord message. | `""` |
| `discord-footer` | Newline-separated lines appended at the very end of the Discord message. | `""` |

| Secret | Description |
|---|---|
| `DISCORD_WEBHOOK_URL` | Discord webhook URL. |

### `publish-artifact-combined.yml` — Publish Combined Artifact

Publishes a .NET project for multiple runtimes, merges all outputs into a single zip, computes its SHA256 (written to the job summary), and uploads it to the GitHub Release.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path/name of the project to publish (e.g. src/MyApp or MyApp) | |
| `artifact-name` | *(required)* Base name for the generated zip file (e.g. MyPlugin) | |
| `artifact-root` | Directory inside the zip to place the merged output under (e.g. MyPlugin). Leave empty to place the output at the root of the zip. | `''` |
| `artifact-ignore` | Newline-separated list of file patterns to exclude from the zip (e.g. `*.pdb`). Patterns containing '/' are matched against the relative path, others against the file name. | `''` |
| `build-matrix` | JSON array of `{os, runtime, executable-extension}` objects | `[{"os":"windows-latest","runtime":"win-x64","executable-extension":".exe"},{"os":"ubuntu-latest","runtime":"linux-x64","executable-extension":""}]` |
| `dotnet-version` | .NET version to use | `latest` |
| `artifact-folder` | Intermediate folder name used when merging build artifacts | `Plugin` |

### `publish-artifact-combined-immutable.yml` — Publish Combined Artifact (Immutable Release)

Publishes a .NET project for multiple runtimes and creates a GitHub Release. This workflow merges all runtime outputs into a single zip file and attaches it to a release. It will also post a Discord notification.

The release tag/version is read from the project's `Version` property, or `VersionPrefix` combined with `VersionSuffix` if set.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the project file (e.g. src/MyApp/MyApp.csproj) to extract version and build from. | |
| `artifact-name` | *(required)* Output zip file name without extension (e.g. MyPlugin). | |
| `artifact-root` | Directory inside the zip to place the merged output under (e.g. MyPlugin). Leave empty for root. | `''` |
| `artifact-ignore` | Newline-separated list of file patterns to exclude from the zip (e.g. `*.pdb`). | `''` |
| `build-matrix` | JSON array of `{os, runtime, executable-extension}` objects. | `[{"os":"windows-latest","runtime":"win-x64","executable-extension":".exe"},{"os":"ubuntu-latest","runtime":"linux-x64","executable-extension":""}]` |
| `dotnet-version` | .NET version to use. | `latest` |
| `artifact-folder` | Intermediate folder name used when merging build artifacts. | `Plugin` |
| `publish-release` | Create a GitHub Release (with tag) from the newly built ZIP. | `true` |
| `discord-description` | Newline-separated lines inserted below the heading, before the changelog, in the Discord message. | `""` |
| `discord-footer` | Newline-separated lines appended at the very end of the Discord message. | `""` |

| Secret | Description |
|---|---|
| `DISCORD_WEBHOOK_URL` | Discord webhook URL. |

### `yml-formatter.yml` — YAML Formatter

Runs [`yamlfmt`](https://github.com/google/yamlfmt) to format or lint YAML files. Can optionally auto-commit formatted files back to the branch.

| Input | Description | Default |
|---|---|---|
| `yamlfmt-version` | yamlfmt version to install | `0.21.0` |
| `args` | Arguments passed to yamlfmt | `.` |
| `auto-commit` | Commit and push formatted files back to the branch | `false` |
| `lint` | Run in lint mode (fail if files are not formatted) | `false` |

Requires `contents: write` permission when `auto-commit` is enabled.

### `validate-workflows.yml` — Workflow Linting (internal)

Runs [`action-validator`](https://github.com/mpalmer/action-validator) against changed workflow files on pull requests. This is a repository-maintenance workflow (not a reusable `workflow_call`) that keeps the workflows in this repo themselves valid; it isn't meant to be called from other repositories.
