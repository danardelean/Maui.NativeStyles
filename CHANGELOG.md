# Changelog

All notable changes to this project are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.1.1] - 2026-09-17

### Fixed
- iOS: `Picker` renders as a pull-down value with `chevron.up.chevron.down`, `DatePicker` and `TimePicker` as the compact `tertiarySystemFill` pill, instead of bordered text fields.

## [1.1.0] - 2026-09-17

### Added
- `Expressive` class for `Border` (Android): Material 3 Expressive list group as in Android 16 Settings, `surfaceContainer` with 28 dp corners; `Gap` class for `BoxView` renders the 2 dp gap between rows (hairline separator on iOS).
- `SystemColorRole.GroupContainer` (iOS secondarySystemGroupedBackground / Android colorSurfaceContainer).

## [1.0.1] - 2026-09-17

### Changed
- Android: `GroupedCell` sections are flat, edge-to-edge Material 3 list sections instead of iOS-style inset cards; dividers use the M3 16 dp inset.

## [1.0.0] - 2026-09-17

### Added
- `Maui.NativeStyles` class library (net10.0, net10.0-android, net10.0-ios) with `NativeStyleDictionary`: iOS 26 Liquid Glass styles on iOS, Material 3 styles on Android.
- Typed attached properties: `NativeButton.Kind` / `IsDestructive` / `Size`, `NativeText.Weight`, `NativeEntry.IsPlain`, `NativeShell.TabBarMinimizeBehavior`; the shared `StyleClass` names set them.
- `{native:SystemColor}` markup extension: theme-aware colors resolved from `UIColor` system colors on iOS and Material theme attributes on Android, with a static iOS 26 / Material 3 baseline fallback.
- `UseNativeStyles(o => o.AndroidDynamicColors = true)` to opt into Material You dynamic colors on Android 12+.
- `GlassView` container: `UIGlassEffect` on iOS 26, system-material blur on iOS 15–18, elevated Material surface on Android.
- iOS 26 floating tab bar that minimizes on scroll (`NativeShell.TabBarMinimizeBehavior`).
- Android `Stepper` rendered as two Material 3 outlined icon buttons.
- Sample app with Buttons, Inputs, Selection, Feedback, Lists, Views and Typography pages.
- Unit tests and GitHub Actions workflow (build, test, pack).
