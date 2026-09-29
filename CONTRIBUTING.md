# Contributing

Contributions are welcome: new controls, platform fixes, token updates when Apple or Google revise their guidelines.

## Workflow

This repository follows [gitflow](https://nvie.com/posts/a-successful-git-branching-model/):

- `main` holds released versions, tagged `vX.Y.Z`.
- `develop` is the integration branch. Open pull requests against `develop`.
- Work on `feature/<name>` branches; releases and hotfixes use `release/` and `hotfix/` branches.

## Guidelines

- Keep the subtractive principle: prefer removing a setter over overriding native rendering. Add a handler mapper only when XAML cannot express the native behavior.
- Every value must be traceable to a source: Apple Human Interface Guidelines, Material 3 tokens, or a measured native control. Cite it in a comment or in the pull request.
- New `StyleClass` names must work on both platforms and be added to the table in the README.
- Verify on an iOS 26 simulator and an Android emulator with `UseMaterial3` before opening the pull request; include screenshots for visual changes.
- Guard every iOS 26 API with `OperatingSystem.IsIOSVersionAtLeast(26)` so the same binary still runs on iOS 15–18.

## Releasing

1. Branch `release/X.Y.Z` from `develop`, set `<Version>` in `src/Maui.NativeStyles/Maui.NativeStyles.csproj` and add a `## [X.Y.Z] - YYYY-MM-DD` section to `CHANGELOG.md`.
2. Merge the release branch into `main` and back into `develop` with `--no-ff`, tag the `main` merge `vX.Y.Z` and push both branches and the tag.
3. The Build workflow on the tag builds, tests and packs, then its release job creates the GitHub release (the CHANGELOG section as notes, the `.nupkg` and `.snupkg` attached) and pushes the package to nuget.org when the `NUGET_API_KEY` repository secret holds a nuget.org API key that can push `Maui.NativeStyles`. Without the secret the job only leaves a notice, and the attached package can be pushed by hand.

The release job fails before publishing anything if the tag does not match `<Version>` or `CHANGELOG.md` has no section for it; fix the release, move the tag and push it again. Re-running the job updates the existing release and skips a package that is already on nuget.org.

The **MAUI canary** workflow builds `develop` every Monday against the newest MAUI service release, and against the next major version without failing the run. The library reaches some MAUI internals through reflection, so a failure there usually means MAUI renamed or removed one of them.

## Reporting issues

Open an issue with the platform, OS version, `Microsoft.Maui.Controls` version, a screenshot and, when possible, the XAML that reproduces it.
