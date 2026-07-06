# Project Guidelines

This repository hosts reusable GitHub Actions workflows (`.github/workflows/*.yml`) consumed by other repos via `workflow_call`.

## Conventions

- [README.md](../README.md) is the single source of truth for what each workflow does — its inputs, secrets, defaults, and required permissions. It must stay in sync with the workflow files.
- Whenever you add, remove, or change a workflow's `inputs`, `secrets`, `permissions`, or overall behavior, update the corresponding section in [README.md](../README.md) in the same change — do not wait to be asked.
- New workflow files need a new `###` section in the README following the existing table format (Input/Description/Default, Secret/Description).
- Repository-maintenance workflows (triggered by `pull_request`/`push`, not `workflow_call`) should be noted as internal/non-reusable, like `validate-workflows.yml`.
