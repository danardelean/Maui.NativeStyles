# Changelog

All notable changes to this project are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.4.0] - 2026-09-17

Android audit against the current Material 3 Expressive component specs on m3.material.io.

### Changed
- Android: `GroupedCell` is now the M3 Expressive **segmented list** (baseline lists are "not recommended" by the spec): transparent group with 16 dp outer corners, `ListRow` items on `surfaceBright` with 4 dp corners, 16×10 dp padding and 12 dp slot spacing; the selected item morphs to `secondaryContainer` with 16 dp corners. `Separator` (and `Gap`) render the 2 dp segment gap. `Expressive` on `Border` is kept as a no-op.
- Android: `Grouped` pages use `surfaceContainer` for the page and the app bar (including the strip behind the status bar); `SystemColorRole.GroupedBackground` → `colorSurfaceContainer`, `GroupContainer` → `colorSurfaceBright`.
- Android: Shell bottom navigation follows the **flexible navigation bar**: 64 dp, 56×32 dp active indicator, 6 dp vertical padding, `secondary` active label, `surfaceContainer` container.
- Android: buttons use the M3 Expressive paddings (16 dp default, 12 dp `Small`, 24 dp `Large`), `Large` is fully round with title-medium text, `Outlined` uses `outlineVariant` / `onSurfaceVariant`.
- Android: `GlassView` uses the floating-toolbar container color (`surfaceContainer`).

### Added
- `NativeList.ItemCornerRadius` attached property (Android: rounded item container; no-op on iOS).
- `Divider` class for `BoxView`, `Leading` class for `CheckBox`; native `RadioButton` rows are list items aligned on the 16 dp keyline.

### Fixed
- Android: `SearchBar` and `Shell.SearchHandler` render the M3 search bar (56 dp pill, `surfaceContainerHigh`) instead of an underlined field / elevated white card.
- Android: `Plain` entries inside list rows no longer inflate the row height.

## [1.3.0] - 2026-09-17

### Added
- `Expressive` class for `Entry` and `Editor` (`NativeEntry.IsContained`): on Android a borderless field in a filled `surfaceContainerHighest` container with 28 dp corners, the containment pattern of the Material 3 Expressive compose concepts. No-op on iOS, where the default field already has this shape.
- `Plain` class for `Editor`.

### Fixed
- Android: `Editor` no longer shows the Material 2 underline (MAUI renders it as a bare `TextInputEditText`); it gets the Material 3 outlined container: 4 dp corners, 1 dp `outline`, 2 dp `primary` when focused, 16 dp padding.

## [1.2.0] - 2026-09-17

### Changed
- iOS: `Entry` no longer uses the legacy `UITextBorderStyle.RoundedRect`. It renders like an iOS 26 Settings text row: borderless, 52 pt tall, filled with `secondarySystemGroupedBackground`, 26 pt continuous corners (a capsule for a single line), 20 pt text inset and a clear button while editing. `Editor` gets the same shape and insets. `Plain` keeps the bare field for grouped rows. On a non-grouped page set `BackgroundColor="{native:SystemColor Fill}"`.
- iOS: `Entry` is created by `NativeEntryHandler` (a plain `EntryHandler` subclass; `EntryHandler.Mapper` customizations keep working) so the text field can inset its text and clear button.
- iOS: `SectionHeader` follows iOS 26 (17 pt semibold, sentence case, aligned with the row text) instead of the uppercase footnote of earlier releases; `GroupedCell` margins are 20 pt and `ListRow` / `RadioButton` rows are 52 pt, as measured in Settings.
- `SectionHeader` / `SectionFooter` margins now carry the whole spacing between sections; use them in a stack with `Spacing="0"`.

### Added
- `ContentMargin` resource (`Thickness`): 20 pt on iOS, 16 dp on Android.
- Documentation and sample: navigation-bar search (`Shell.SearchHandler`) as the native search field on iOS 26.

## [1.1.3] - 2026-09-17

### Fixed
- iOS: inset grouped sections and cards use the iOS 26 corner radius (26 pt); `DatePicker`/`TimePicker` compact pills are 34 pt capsules; row separators are inset 16 pt on both sides, matching Settings.

## [1.1.2] - 2026-09-17

### Fixed
- Android: `Picker`, `DatePicker` and `TimePicker` no longer show the Material 2 underline; the value is secondary text with a trailing menu arrow, calendar or clock icon, as in Material list rows.

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
