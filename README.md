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

Publishes a Blazor WebAssembly project and deploys it to GitHub Pages. Automatically patches `<base href>` and `service-worker.published.js` to the repository name.

Requires `pages: write` and `id-token: write` permissions on the caller.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the Blazor WASM project | |
| `dotnet-version` | .NET version | `latest` |
| `workloads` | Comma-separated workloads to install | `wasm-tools` |
| `enable-cache` | Cache NuGet packages | `true` |

### `docker-publish.yml` — Publish Docker Images

Builds and pushes Docker images to Docker Hub and/or GitHub Container Registry using a matrix of projects.

| Input | Description | Default |
|---|---|---|
| `matrix` | *(required)* JSON array of `{project, image_name}` objects | |
| `platforms` | Target platforms | `linux/amd64,linux/arm64` |
| `dockerfile-prefix` | Path prefix to locate Dockerfiles | `Src/` |
| `push-to-dockerhub` | Push to Docker Hub | `true` |
| `push-to-ghcr` | Push to GitHub Container Registry | `true` |
| `dockerhub-username` | Docker Hub username | |

| Secret | Description |
|---|---|
| `DOCKERHUB_TOKEN` | Docker Hub access token |

### `publish-nuget.yml` — Publish NuGet Packages

Builds, tests, packs, and publishes NuGet packages to nuget.org and/or GitHub Packages.

| Input | Description | Default |
|---|---|---|
| `package-prefix` | Filter packages by prefix (e.g. `MyCompany.`) | |
| `dotnet-version` | .NET version | `latest` |
| `enable-coverage` | Generate and publish coverage summary | `true` |
| `push-to-nuget` | Publish to nuget.org | `true` |
| `push-to-github` | Publish to GitHub Packages | `true` |
| `upload-to-release` | Upload `.nupkg` files to the GitHub Release | `true` |

| Secret | Description |
|---|---|
| `NUGET_API_KEY` | nuget.org API key |

### `publish-zip.yml` — Publish Per-Runtime ZIPs

Publishes a .NET project for multiple runtimes, zips each output separately, computes a SHA256 checksum per zip (written to the job summary), and uploads all zips to the GitHub Release.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the project to publish | |
| `zip-prefix` | *(required)* Prefix for output zip names (e.g. `MyApp`) | |
| `matrix` | JSON array of `{os, runtime}` objects | `win-x64` + `linux-x64` |
| `dotnet-version` | .NET version | `latest` |

### `publish-zip-combined.yml` — Publish Combined ZIP

Publishes a .NET project for multiple runtimes, merges all outputs into a single zip, computes its SHA256 (written to the job summary), and uploads it to the GitHub Release.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Project name/path to publish | |
| `zip-name` | *(required)* Output zip name without extension | |
| `matrix` | JSON array of `{os, runtime, executable-extension}` objects | `win-x64` + `linux-x64` |
| `dotnet-version` | .NET version | `latest` |
| `artifact-folder` | Intermediate merge folder name | `Plugin` |
