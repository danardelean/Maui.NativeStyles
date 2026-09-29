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

## Testing

Three layers, from the fastest to the closest to a real app.

### Unit tests

```bash
dotnet test tests/Maui.NativeStyles.Tests/Maui.NativeStyles.Tests.csproj
```

Plain `net10.0`, no device; CI runs them. `ReflectionCanaryTests` pins the MAUI internals the library reaches by
reflection (`AppThemeBinding`, `Shell._appearanceObservers`): when one fails after a MAUI update, fix the reflection code
before bumping `MauiVersion`.

### Device tests

`tests/Maui.NativeStyles.DeviceTests` is a MAUI app that runs xunit tests on a booted iOS simulator or Android emulator
([DeviceRunners](https://github.com/mattleibow/DeviceRunners)), with the real handlers and native views: button
configurations, text field shapes, `SegmentedControl` segments, `SystemColors` against UIKit and the Material theme, and a
canary for MAUI's internal Android `*Handler2` mappers.

```bash
dotnet test tests/Maui.NativeStyles.DeviceTests -f net10.0-ios -p:DeviceRunnersDevice=<simulator UDID>
dotnet test tests/Maui.NativeStyles.DeviceTests -f net10.0-android -p:DeviceRunnersDevice=emulator-5554
```

The app is built, deployed and run headlessly; the results come back over TCP (port 16384) into
`tests/Maui.NativeStyles.DeviceTests/TestResults` (TRX, event stream, device log) or `--results-directory`. `--filter`
works as usual. Exit code 1 means failed tests, 2 means the app crashed (partial results). The device is required so that
`dotnet test` never deploys to whatever simulator happens to be running. On Android, set `ANDROID_SERIAL` as well when
several devices are attached; the runner clears the device's logcat. Started from the IDE, the app shows the interactive
visual runner.

### Visual regression check

`scripts/visual-check.py` (Python 3, standard library only) builds and installs the sample, sets a 9:41 status bar
(simulator override, Android demo mode), opens every page in light and dark through the sample's `nativestyles://` links
and compares the screenshots with `tests/visual/baselines/<platform>/<page>-<theme>.png`.

```bash
scripts/visual-check.py --ios <simulator UDID> --android emulator-5554   # compare
scripts/visual-check.py --ios <simulator UDID> --update                  # accept the new look
scripts/visual-check.py --android emulator-5554 --pages buttons,inputs --themes dark --no-build
```

- Screenshots are reduced by an integer factor to about 400 px wide. A pixel counts as changed when a channel differs by
  more than `--threshold` (24/255); a page fails above `--max-diff` (0.05 % of its pixels). Areas that change on their
  own (system chrome) are listed in `tests/visual/masks.json`.
- The report (`report.html` with baseline / current / diff images, `report.md`) goes to `artifacts/visual-check` or
  `--output`. Exit code 0 = no differences, 1 = differences or missing baselines, 2 = device or build errors.
- Baselines depend on the device: `device.json` next to them records the model, OS and screen they were taken on
  (iPhone 17 Pro, iOS 26.5; Pixel 10 Pro AVD, Android 16, 1280×2856 at 480 dpi). The script warns on a mismatch; use the
  same devices or regenerate with `--update`.
- After an intended visual change, run `--update`, review the images and commit them with the change.
- The script restores what it changes (status bar, simulator appearance, Android demo mode) and leaves the emulator's
  night mode alone: the theme is set in the app (`Application.UserAppTheme`).

The links also work by hand: `nativestyles://page/<buttons|inputs|selection|feedback|lists|views|typography|flyout>?theme=light|dark`
and `nativestyles://theme/<light|dark|system>`.

```bash
adb shell am start -a android.intent.action.VIEW -d "nativestyles://page/inputs?theme=dark" it.mahiz.mauinativestyle
xcrun simctl launch --terminate-running-process <UDID> it.mahiz.mauinativestyle -NativeStylesLink "nativestyles://page/inputs?theme=dark"
```

(`xcrun simctl openurl` works too, but iOS asks for confirmation before opening a custom scheme.)

## Releasing

1. Branch `release/X.Y.Z` from `develop`, set `<Version>` in `src/Maui.NativeStyles/Maui.NativeStyles.csproj` and add a `## [X.Y.Z] - YYYY-MM-DD` section to `CHANGELOG.md`.
2. Merge the release branch into `main` and back into `develop` with `--no-ff`, tag the `main` merge `vX.Y.Z` and push both branches and the tag.
3. The Build workflow on the tag builds, tests and packs, then its release job creates the GitHub release (the CHANGELOG section as notes, the `.nupkg` and `.snupkg` attached) and pushes the package to nuget.org when the `NUGET_API_KEY` repository secret holds a nuget.org API key that can push `Maui.NativeStyles`. Without the secret the job only leaves a notice, and the attached package can be pushed by hand.

The release job fails before publishing anything if the tag does not match `<Version>` or `CHANGELOG.md` has no section for it; fix the release, move the tag and push it again. Re-running the job updates the existing release and skips a package that is already on nuget.org.

The **MAUI canary** workflow builds `develop` every Monday against the newest MAUI service release, and against the next major version without failing the run. The library reaches some MAUI internals through reflection, so a failure there usually means MAUI renamed or removed one of them.

## Reporting issues

Open an issue with the platform, OS version, `Microsoft.Maui.Controls` version, a screenshot and, when possible, the XAML that reproduces it.
