# Workflow guidelines

This repository hosts reusable GitHub Actions workflows for .NET projects. Other repositories call them with `workflow_call`.

For documentation wording, see [Writing style](writing-style.md).

## Workflows are YAML

A workflow should read top to bottom as a list of steps. Each step does one thing, usually a single command or an existing action.

- Keep `run` blocks to one command, or a few closely related commands.
- Use expressions (`${{ }}`) for simple choices like adding an argument when an input is set. Split a step in two with `if` when the command changes more than that.
- Prefer an existing, maintained action over reimplementing it.
- Use `shell: bash` (set in `defaults.run`) so steps behave the same on every runner.
- Add a comment above a step only when the reason for it isn't obvious.

When a step needs loops, parsing, retries, API calls, or more than a few conditions, it is too complex for shell. Write it in C# instead.

## C# for complex logic

Complex logic goes into [`Workflows.Tools`](../tools/Workflows.Tools), a console app with one command per workflow step, such as `nuget pack`. Commands are grouped by workflow in folders (`NuGet/`, `PullRequests/`), and code that several workflows can share has its own folder (`GitHub/`, `Discord/`, `Processes/`). Use `GitHubApi` for GitHub REST API calls.

A reusable workflow runs in the caller's repository, so it can't reach this repository's files through a normal checkout. [`publish-nuget-immutable.yml`](../.github/workflows/publish-nuget-immutable.yml) shows the steps to copy:

1. Check out `tools` at the exact commit of the workflow, with `repository: ${{ job.workflow_repository }}` and `ref: ${{ job.workflow_sha }}`.
2. Move it to `$RUNNER_TEMP`, so the caller's `global.json` and MSBuild files don't affect it and its projects don't match the caller's globs.
3. Build it once with .NET 10, and set `WORKFLOW_TOOLS` to the built DLL.
4. Run each command as `dotnet "$WORKFLOW_TOOLS" <command>`, with inputs passed as environment variables.

act doesn't provide `job.workflow_*`, so under act (`env.ACT`) the tools are copied from the workspace instead. That works because act only runs this repository's own tests.

Write the code so it can be tested without a runner or network access:

- Read inputs and write outputs and the job summary through `ActionsContext`.
- Run processes through `IProcessRunner` and send HTTP requests through an injected `HttpClient`, so tests can replace them with fakes.
- Throw `WorkflowException` for expected failures. The message becomes an error annotation.
- Keep commands thin. Put logic that tests need to check in its own class or method, like `Announcement` or `PushOutput`.

## Testing

[`test.yml`](../.github/workflows/test.yml) is the test suite. It runs on pull requests and pushes to `main`.

- **Lint:** [actionlint](https://github.com/rhysd/actionlint) checks every workflow file, including expressions and `run` blocks.
- **End-to-end:** each reusable workflow is called with fixture projects from [`tests/fixtures`](../tests/fixtures). A run passes when the workflow succeeds on the fixture.

Run the suite locally with `act workflow_dispatch` before pushing (see [Testing locally](../README.md#testing-locally)). Verify every workflow change this way. Linux-only problems and broken steps show up locally, while Windows-specific behavior only shows up on GitHub.

Fixtures are small, real .NET solutions. Add one when a workflow gains a mode that changes its commands, such as a different test platform. Keep each fixture minimal, with just enough code to exercise that mode.

A workflow should fail loudly when something it was asked to do produced nothing, so a broken fixture can't pass the test.

### Publishing workflows

Publishing workflows talk to external services, so their end-to-end tests must not publish anything real. Design them to be pointed at a safe target:

- Expose the destination as an input, such as a NuGet source URL. Tests can push to a local folder feed instead, like [`tests/fixtures/nuget/feed`](../tests/fixtures/nuget/feed). A folder feed always reports a new upload, so duplicate handling is covered by unit tests.
- Gate side effects such as releases and announcements behind boolean inputs, so tests can turn them off.
- Expose results as workflow outputs, so a follow-up job in `test.yml` can check them.
- Test the C# logic behind them (release notes, version detection, API calls) with unit tests that use fakes for every external call.

Unit tests for the tools live in [`Workflows.Tools.Tests`](../tools/Workflows.Tools.Tests) and run in `test.yml`. `TestRunner` gives each test its own workspace, temp directory, step outputs, and job summary. `FakeProcessRunner` and `FakeHttpHandler` record calls and return configured results, and `FakeGitHub` answers API requests for one pull request.

Workflows that change pull requests or issues should have a dry-run input. `test.yml` can then run them against a real pull request without changing it.

## Documentation

[README.md](../README.md) documents every reusable workflow: what it does, its inputs, secrets, and required permissions. Update it in the same change as the workflow. Internal workflows such as `test.yml` are noted as not reusable.
