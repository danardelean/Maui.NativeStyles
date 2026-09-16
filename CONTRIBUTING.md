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

## Reporting issues

Open an issue with the platform, OS version, `Microsoft.Maui.Controls` version, a screenshot and, when possible, the XAML that reproduces it.
