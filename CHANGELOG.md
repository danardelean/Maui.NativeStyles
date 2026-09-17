# Changelog

All notable changes to this project are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses [Semantic Versioning](https://semver.org/).

## [1.8.0] - 2026-09-17

### Added
- Android: **runtime theme switching**. `NativeStylesOptions.AndroidRecreateOnThemeChange` (on by default) recreates the activity when the effective app theme changes (system dark mode or `Application.UserAppTheme`), so native widgets, the navigation bar, status bar icons, dialogs and the brand scheme are re-themed; previously only `{native:SystemColor}` values updated. The page that was showing is moved to the recreated window, so navigation state, the selected tab, control values and view models survive even with the template's `new Window(new AppShell())`.
- The sample has an in-app light / dark toggle.

### Fixed
- Android: replaced root pages (for example a Shell swapped for another page) are disconnected before the recreate, and the dead appearance observer MAUI 10 leaves on a replaced Shell is cleared; it caused a `NullReferenceException` inside MAUI on the following theme change.
- Android: the `GlassView` theme handler no longer uses throwing accessors on a disconnected handler; themed contexts cached for `{native:SystemColor}` are dropped with their activity.

## [1.7.0] - 2026-09-17

### Added
- **Branding**: `NativeStylesOptions.Brand` (`BrandPalette`). One brand color serves both platforms: on iOS it becomes the window tint (with an optional dark-mode variant) and the accent roles; on Android it is the seed of a generated Material 3 scheme (light and dark, tinted surfaces included) that is resolved by every `{native:SystemColor}` role and also replaces Material's color resources for each activity (Android 11+), so native widgets, the navigation bar, tabs and dialogs are branded. Per-platform overrides (`Brand.IOS`, `Brand.Android`), per-role pins (`Brand.Set`) and `MaterialColorMatch` (exact brand color as light-theme primary).
- The shared `AccentColor` / `DestructiveColor` resources follow the brand; Android typography colors resolve through `{native:SystemColor}`.

### Fixed
- Android: dynamic color overlays are now applied from `OnActivityCreated`. Applied earlier (as `DynamicColors.ApplyToActivitiesIfAvailable` does), the overlay is discarded by MAUI's `SetTheme()` call in `OnCreate`; this was observed with the brand seed and the same path serves `AndroidDynamicColors`.

### Changed
- The sample application sets a brand palette.

## [1.6.4] - 2026-09-17

### Fixed
- Android: `TabbedPage` with top tabs kept MAUI's primary-colored strip and untinted icons. Tabs now paint their own container in the app-bar color and tint icons like the labels (`primary` when selected, `onSurfaceVariant` otherwise).

### Notes
- Documented that MAUI's `SetToolbarPlacement()` throws after the library style has set `Bottom`; use the XAML attribute or `SetValue` for top tabs. The sample gains a top-tabs `TabbedPage` demo and an `EmptyState` example.

## [1.6.3] - 2026-09-17

### Changed
- Android: the Shell flyout / `FlyoutPage` sheet follows the M3 modal navigation drawer: at most 360 dp wide leaving 56 dp of scrim, 16 dp corners on the trailing side, and a uniform `surfaceContainerLow` background (the body previously stayed `surface`).
- Android: primary tabs draw the 1 dp `outlineVariant` divider inside the tab row.
- Android: trailing toolbar icons and the overflow glyph use `onSurfaceVariant` on a themed (surface) app bar; the navigation icon stays `onSurface`.

## [1.6.2] - 2026-09-17

### Added
- `Icon` class for `ImageButton`, and `NativeImage.TintColor` now also applies to `ImageButton`: the image is drawn as a template (iOS tint color / Material `onSurfaceVariant`), so monochrome icons stay visible in dark mode.

## [1.6.1] - 2026-09-17

### Fixed
- iOS: the hairline MAUI draws under the Shell top-tabs strip is hidden (iOS 26 has no separator under the segmented control).

## [1.6.0] - 2026-09-17

### Added
- `NativeSwipeItem` (`SwipeItemView`): swipe actions drawn like the platform instead of MAUI's square color blocks. iOS 26: separated, continuously rounded actions; Android: M3 swipe-to-reveal round tonal / primary / error buttons. `SwipeRole` (`Default`, `Primary`, `Destructive`).
- `SegmentedControl`: `UISegmentedControl` on iOS, Material 3 Expressive connected button group on Android.
- `NativeImage.TintColor` attached property (template tinting of bitmap images on both platforms) and `SystemColorRole.Gray`.
- Shell flyout styling through `Shell.ItemTemplate` / `Shell.MenuItemTemplate` and the `FlyoutHeader` label class: iOS sidebar look, Android M3 navigation drawer.
- Shell top tabs: a `UISegmentedControl` replaces MAUI's underlined strip on iOS; M3 primary tabs on Android (also for `TabbedPage` top tabs).
- `TabbedPage` / `NavigationPage` styles for apps without Shell: Android bottom navigation bar with the flexible metrics, content margin kept in sync with the 64 dp bar, flat app bar in the page color; iOS transparent navigation bar in the page color, and `NativeShell.TabBarMinimizeBehavior` now works on `TabbedPage`.
- `RefreshView` style (Android) and the `EmptyState` label class for `CollectionView.EmptyView`.

### Notes
- The legacy `ListView`, `TableView` and `Frame` controls are intentionally left unstyled.

## [1.5.0] - 2026-09-17

### Added
- `CarouselView` implicit styles and the `CarouselItem` class for `Border`. iOS: leading-aligned paging (card on the 20 pt content margin, 12 pt gap, next card peeking from the trailing edge). Android: Material 3 *uncontained* carousel (16 dp leading padding, 8 dp gaps, 28 dp item corners, items bleed off the trailing edge). Carousels no longer loop. MAUI centers the current item and averages the peek insets, so the alignment uses a symmetric peek plus a negative leading margin.

### Changed
- `IndicatorView`: on iOS the native `UIPageControl` is no longer scaled (`IndicatorSize` 6) and uses `label` / `tertiaryLabel`; both platforms center it and leave 8 units above it.

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
