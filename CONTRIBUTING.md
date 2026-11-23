# Contributing

## Issues

Every change starts from a Jira issue with a key like `HK-31`. The key goes
into the branch name, the commit messages and the pull request title.

## Branches

Create a branch from `main` named after the issue:

```
HK-<number>-short-description
```

Use lowercase words separated by hyphens, for example
`HK-16-fix-build-and-entry-label` or `HK-22-docker-postgres`.

## Commits

Start every commit message with the issue key, a colon and a short summary in
the imperative mood:

```
HK-26: Implement auth hash derivation (HKDF)
```

If a commit covers more than one issue, list all keys: `HK-11, HK-25: ...`.

## Pull requests

- Open a pull request into `main`. The title follows the commit format, e.g.
  `HK-31: Add .editorconfig`.
- Fill in the template: Jira issue, what changed, how it was tested.
- CI must pass. Warnings are treated as errors, so fix them instead of
  suppressing them.
- Get at least one review before merging.

## Code style

Formatting and naming rules live in [`.editorconfig`](.editorconfig). Naming
and the other rules marked `warning` fail the build. Visual Studio, Rider and VS Code (with the
EditorConfig and C# extensions) apply them automatically. To format the files
you changed from the command line:

```
dotnet format Hashkeeper.slnx --include <paths>
```
