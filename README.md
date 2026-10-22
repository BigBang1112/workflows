# Workflows

Reusable GitHub Actions workflows for .NET projects. Call them from any repository with `workflow_call`.

## Usage

```yaml
jobs:
  ci:
    uses: bigbang1112/workflows/.github/workflows/dotnet-ci.yml@main
    with:
      dotnet-version: 10.0.x
```

Pin `@main` to a tag or commit SHA if you want the workflow to stay the same until you update it.

## Workflows

### `dotnet-ci.yml`

Restores, builds, and tests a .NET solution or project on one or more runners, and collects Cobertura code coverage with both VSTest and Microsoft Testing Platform (MTP).

| Input | Description | Default |
|---|---|---|
| `dotnet-version` | .NET SDK version to install. When empty, the SDK from `global.json` in the repository root is used, or the runner's preinstalled SDK if there is none. | `""` |
| `workloads` | Comma-separated workloads to install (e.g. `wasm-tools`) | `""` |
| `os` | JSON array of runners | `["ubuntu-latest", "windows-latest"]` |
| `working-directory` | Directory with the solution or project, relative to the repository root | `.` |
| `configuration` | Build configuration | `Release` |
| `build-args` | Extra `dotnet build` arguments | `""` |
| `test-args` | Extra `dotnet test` arguments | `""` |
| `enable-tests` | Run `dotnet test` | `true` |
| `enable-coverage` | Collect Cobertura coverage into `$RUNNER_TEMP/coverage` | `true` |
| `enable-cache` | Cache NuGet packages | `true` |

### Coverage

Both test platforms use Microsoft code coverage, so the reports have the same format and content. Test assemblies are excluded, and only the code under test is measured.

| Platform | Package needed | Report location |
|---|---|---|
| VSTest | None, `Microsoft.NET.Test.Sdk` already includes it | `$RUNNER_TEMP/coverage/[guid]/[name].cobertura.xml` |
| MTP | `Microsoft.Testing.Extensions.CodeCoverage` (MSTest.Sdk includes it by default) | `$RUNNER_TEMP/coverage/[guid].cobertura.xml` |

Use `**/*.cobertura.xml` to find the reports from either platform. With MTP, **`dotnet test` fails if the coverage package is missing**, because it doesn't recognize `--coverage`. Set `enable-coverage: false` for projects without it.

### Test platform detection

The workflow detects the test platform the same way `dotnet test` does, from the test runner in `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

It looks for `global.json` in `working-directory` and in the repository root. A `global.json` in a directory between them isn't detected, so put it in one of those two places.

### `blazor-wasm-github-pages.yml`

Publishes a Blazor WebAssembly app and deploys it to GitHub Pages. Before publishing, it sets the base path in `wwwroot/index.html` and, for PWAs, in `wwwroot/service-worker.published.js`, so the app works when it isn't served from the domain root.

```yaml
jobs:
  pages:
    uses: bigbang1112/workflows/.github/workflows/blazor-wasm-github-pages.yml@main
    permissions:
      contents: read
      pages: write
      id-token: write
    with:
      project: src/MyApp
```

In the repository settings, set **Pages > Build and deployment > Source** to **GitHub Actions**.

| Input | Description | Default |
|---|---|---|
| `project` | *(required)* Path to the Blazor WebAssembly project directory (e.g. `src/MyApp`) | |
| `dotnet-version` | .NET SDK version to install. When empty, the SDK from `global.json` in the repository root is used, or the runner's preinstalled SDK if there is none. | `""` |
| `workloads` | Comma-separated workloads to install | `wasm-tools` |
| `base-path` | Path the site is served from, with a trailing slash (e.g. `/my-repo/`). When empty, it is read from the repository's Pages settings. | `""` |
| `enable-cache` | Cache NuGet packages | `true` |
| `deploy` | Deploy the site. When `false`, the site is only built and uploaded as the `github-pages` artifact. | `true` |

| Output | Description |
|---|---|
| `page-url` | URL of the deployed site |

The default base path comes from the Pages settings, so it is `/my-repo/` for a project site and `/` for a custom domain or a `username.github.io` repository. **The build fails if `index.html` or the service worker doesn't contain the default base** (`<base href="/" />` and `const base = "/";`), so a changed template can't deploy a broken site.

Deployments run one at a time per repository. A new deployment waits for the running one to finish instead of cancelling it.

### `publish-nuget-immutable.yml`

Builds, tests, packs, and publishes .NET packages to NuGet.org, GitHub Packages, and custom feeds. It then creates one GitHub release for the packages that were uploaded for the first time, which works with [immutable releases](https://docs.github.com/repositories/releasing-projects-on-github/about-releases), and can announce it on Discord.

```yaml
on:
  push:
    tags: ["v*"]

jobs:
  publish:
    uses: bigbang1112/workflows/.github/workflows/publish-nuget-immutable.yml@main
    permissions:
      contents: write
      packages: write
    with:
      package-prefix: MyCompany.
      main-project: MyCompany.Core
    secrets:
      NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
```

| Input | Description | Default |
|---|---|---|
| `package-prefix` | Only publish packages whose ID starts with this prefix (e.g. `MyCompany.`) | `""` |
| `project-path` | Newline-separated project paths or globs to build | `""` (repository root) |
| `test-path` | Newline-separated test project paths or globs | `""` (repository root) |
| `pack-path` | Newline-separated project paths or globs to pack | `""` (`project-path`, then repository root) |
| `dotnet-version` | .NET SDK versions to install, one per line. Install every version the test projects target. When empty, the SDK from `global.json` in the repository root is used, or the runner's preinstalled SDK if there is none. | `""` |
| `workloads` | Comma-separated workloads to install | `""` |
| `enable-tests` | Run tests | `true` |
| `enable-coverage` | Collect Cobertura coverage into `$RUNNER_TEMP/coverage` when tests run | `true` |
| `push-to-nuget` | Publish to NuGet.org | `true` |
| `push-to-github` | Publish to GitHub Packages | `true` |
| `push-to-custom-feeds` | Publish to `custom-feed-urls` (requires `CUSTOM_FEED_API_KEYS`) | `false` |
| `custom-feed-urls` | Newline-separated feed URLs. A relative folder path is resolved from the repository root. | `""` |
| `upload-to-release` | Attach the newly uploaded `.nupkg` and `.snupkg` files to the release | `true` |
| `main-project` | Package ID or project directory name of the main package (see [Release notes](#release-notes)) | `""` |
| `create-release` | Create a GitHub release for the newly uploaded packages | `true` |
| `release-description` | Text placed before the release notes in the release | `""` |
| `send-discord-message` | Post the release notes to Discord after the release is created | `false` |
| `discord-top-lines` | Newline-separated lines inserted below the heading, before the release notes, in the Discord message | `""` |
| `discord-bottom-lines` | Newline-separated lines appended at the very end of the Discord message | `""` |
| `release-title-prefix` | Text before the version in the release title, separated by one space | `""` |

| Secret | Description |
|---|---|
| `NUGET_API_KEY` | NuGet.org API key |
| `CUSTOM_FEED_API_KEYS` | Newline-separated API keys in the same order as `custom-feed-urls` |
| `DISCORD_WEBHOOK_URL` | Discord webhook URL for the release announcement |

| Output | Description |
|---|---|
| `has-new` | `true` when at least one package was uploaded for the first time |
| `release-url` | URL of the created GitHub release |

#### Several packages

A repository with libraries in `Src`, tests in `Tests`, and a template package next to them can publish everything in one release. The test projects target several frameworks, so every SDK they need is installed. A `workflow_dispatch` description goes to both the release and the Discord message:

```yaml
on:
  push:
    tags: ["v*"]
  workflow_dispatch:
    inputs:
      description:
        description: Text before the release notes
        type: string
        default: ""

jobs:
  publish:
    uses: bigbang1112/workflows/.github/workflows/publish-nuget-immutable.yml@main
    permissions:
      contents: write
      packages: write
    with:
      dotnet-version: |
        10.0.x
        9.0.x
        8.0.x
      project-path: Src/*/*.csproj
      test-path: Tests/*/*.csproj
      pack-path: |
        Src/*/*.csproj
        Templates/MyLibrary.Templates.csproj
      main-project: MyLibrary
      release-title-prefix: MyLibrary
      release-description: ${{ inputs.description }}
      push-to-custom-feeds: true
      custom-feed-urls: https://nuget.example.com/v3/index.json
      send-discord-message: true
      discord-top-lines: ${{ inputs.description }}
    secrets:
      NUGET_API_KEY: ${{ secrets.NUGET_API_KEY }}
      CUSTOM_FEED_API_KEYS: ${{ secrets.MY_FEED_API_KEY }}
      DISCORD_WEBHOOK_URL: ${{ secrets.DISCORD_WEBHOOK_URL }}
```

#### Publishing

Every package is pushed to each selected feed, and its `.snupkg` symbols go to NuGet.org. The job summary has a table with the result for each feed:

- `uploaded`: the feed confirmed a new upload.
- `duplicate`: the feed already has this version.
- `failed`: the push failed, or the feed's API key is missing.
- `unconfirmed`: the push succeeded, but the output doesn't confirm a new upload.

**Only `uploaded` makes a package part of the release.** Rerunning the workflow after a partial failure releases only what is new, and a symbols-only upload doesn't count. When any feed reports `failed` or `unconfirmed`, the job still creates the release for the uploaded packages and then fails.

The packed packages are also kept as the `nuget-packages` artifact.

#### Release notes

Each package's notes come from the `PackageReleaseNotes` property of the project that produced it. The workflow matches packages to projects by the package's actual ID and version (`PackageVersion`, which `VersionPrefix` and `VersionSuffix` set by default), and removes the indentation from notes written over several lines:

```xml
<PackageReleaseNotes>
  - Added `Calculator.Add`
    - Works with negative numbers
</PackageReleaseNotes>
```

`release-description` comes first. With `main-project`, that package's notes follow without a heading, and its version names the release. Every other package gets a `### [ID] [Version]` heading. Without it, every package has a heading and the highest version names the release.

The release tag is the pushed tag when the workflow runs for one (and the release then requires that tag to exist). Otherwise, it is `v` followed by the main package's version.

#### Discord

The message starts with a `## [ID] [Version]` heading for the main package, followed by `discord-top-lines` and its notes. The other packages follow, then links to the release and, when the main package is on NuGet.org, to its NuGet page. Long messages are split at line breaks into several messages, and rate-limited requests are retried.

### `check-agent-authorship.yml`

Checks a pull request for commits authored or co-authored by AI agents:

- **Co-authored:** some commits name an agent as their author or in a `Co-authored-by` trailer. The workflow comments with a table of those commits and the steps to rewrite them, and the check fails until they're fixed.
- **Agent only:** an agent opened the pull request, or authored every commit. The workflow comments and closes the pull request.
- **None:** the check passes. A comment from an earlier run is updated to say the problem is resolved.

The workflow keeps a single comment per pull request and edits it on every run, so pushing fixes doesn't add more comments.

```yaml
on:
  pull_request_target:
    types: [opened, reopened, synchronize]

jobs:
  agents:
    uses: bigbang1112/workflows/.github/workflows/check-agent-authorship.yml@main
    permissions:
      contents: read
      pull-requests: write
```

`pull_request_target` lets the workflow comment on and close pull requests from forks. It is safe here, because the workflow only reads commit metadata through the API and never checks out or runs the pull request's code. With `pull_request`, it only works for branches in the same repository.

| Input | Description | Default |
|---|---|---|
| `pull-request` | Number of the pull request to check | The triggering pull request |
| `agent-patterns` | Newline-separated regular expressions for more agent identities | `""` |
| `close-agent-pull-requests` | Close pull requests created entirely by an agent | `true` |
| `dry-run` | Only report the result, without commenting or closing | `false` |

| Output | Description |
|---|---|
| `result` | `none`, `co-authored`, or `agent-only` |

#### Recognized agents

Patterns are case-insensitive regular expressions, matched against GitHub logins and against `Name <email>` of commit authors and co-authors. The defaults recognize:

| Agent | Identity |
|---|---|
| Claude Code | `noreply@anthropic.com` |
| Claude GitHub app | `claude[bot]` |
| GitHub Copilot in editors | `copilot@github.com` |
| GitHub Copilot coding agent | login `Copilot`, `copilot-swe-agent[bot]`, `…+Copilot@users.noreply.github.com` |
| Devin | `devin-ai-integration[bot]` |
| Jules | `google-labs-jules[bot]` |
| Cursor agent | `cursoragent@cursor.com` |
| aider | `(aider)` in the author name |

Add other agents with `agent-patterns`:

```yaml
    with:
      agent-patterns: |
        chatgpt-codex-connector\[bot\]
        @my-agent\.example\.com
```

GitHub lists at most 250 commits for a pull request, so later commits in a bigger one aren't checked.

### `test.yml` (internal)

Tests this repository. It lints every workflow file with [actionlint](https://github.com/rhysd/actionlint), runs the unit tests of the [workflow tools](tools), and runs the reusable workflows against the fixture projects in [`tests/fixtures`](tests/fixtures):

- `blazor-wasm-github-pages.yml` runs without deploying. A follow-up job checks the base path in the uploaded site.
- `publish-nuget-immutable.yml` publishes to a folder feed in the workspace, without a release or Discord message. A follow-up job checks the `has-new` output and the uploaded packages.
- `check-agent-authorship.yml` does a dry run against pull request #41 of this repository. A follow-up job checks that the result is `none`.

The `validate` job passes only when every other job passed. It is the required status check for pull requests into `main`.

It runs on pull requests and pushes to `main` and isn't meant to be called from other repositories.

## Testing locally

You can run `test.yml` on your machine with [act](https://github.com/nektos/act) and Docker. It needs act 0.2.89 or newer, because older versions don't support actions that run on Node 24.

```sh
act workflow_dispatch
```

[`.actrc`](.actrc) sets the runner image and limits the end-to-end tests to `ubuntu-latest`. act can't run Windows jobs in a container. Without this limit, it would run them on Linux and report a pass that doesn't mean anything. Windows is still covered when the workflow runs on GitHub.

To run a single job or case:

```sh
act workflow_dispatch -j lint
act workflow_dispatch -j dotnet-ci --matrix name:MTP
```

> [!WARNING]
> The `blazor-wasm-github-pages` and `publish-nuget-immutable` tests currently fail under act when they upload an artifact. act's artifact server doesn't support `upload-artifact@v7` yet ([nektos/act#6022](https://github.com/nektos/act/issues/6022)). Every step before the upload still runs, including publishing to the test feed, so most problems show up locally. The uploads and the follow-up checks only run on GitHub.

To run the unit tests of the workflow tools without act:

```sh
cd tools
dotnet test
```

## Contributing

See [Workflow guidelines](.agents/workflow-guidelines.md) for how workflows are structured and tested, and [Writing style](.agents/writing-style.md) for documentation.
