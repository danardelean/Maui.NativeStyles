# Maui.NativeStyles

.NET MAUI styles that respect each platform's design language:
**iOS 26 (Liquid Glass, Human Interface Guidelines)** and **Android (Material 3 baseline)**.

The `dotnet new maui` template ships implicit styles (`Styles.xaml`) that destroy the native look:
fixed background and corner radius on `Button`, `BackgroundColor="Transparent"` on inputs (which erases the Material underline/box),
`FontFamily="OpenSansRegular"`, `FontSize 14` everywhere, and custom colors on Switch/Slider/CheckBox/ProgressBar.
This project replaces that file with **one set of ResourceDictionaries per platform** plus a few handler mappings in C#.

| iOS 26 | Android (Material 3) |
|---|---|
| Liquid Glass tab bar and buttons, large titles, inset grouped lists, native switches and alerts | Pill buttons, outlined text fields, M3 switches and checkboxes, navigation bar with active indicator, M3 dialogs |

## Principle

The library is mostly **subtractive**: not setting fonts, colors and radii lets the native rendering through.
When compiled against the iOS 26 SDK, tab bars, navigation bars, alerts, pickers, switches, sliders and search bars are already Liquid Glass.
With `<UseMaterial3>true</UseMaterial3>` on Android, Entry, Switch, Slider, CheckBox, RadioButton, ProgressBar and dialogs are already Material 3.
What gets added are **tokens** (colors, typography, metrics) and opt-in **variants** via `StyleClass`.

## Structure

```
src/Maui.NativeStyles/            the library (net10.0; net10.0-android; net10.0-ios), packable as Maui.NativeStyles
  NativeStyleDictionary.cs        merge this into Application.Resources
  NativeProperties.cs             NativeButton / NativeText / NativeEntry attached properties
  NativeShell.cs                  NativeShell.TabBarMinimizeBehavior (iOS 26)
  SystemColors.cs                 {native:SystemColor} markup extension + SystemColors API
  GlassView.cs                    Liquid Glass container
  Platforms/iOS/                  UIButtonConfiguration / glass mappers, GlassViewHandler, UIColor system colors
  Platforms/Android/              Material 3 mappers (destructive text, plain Entry, Stepper), theme attributes, dynamic colors
  Resources/iOS/                  iOSColors.xaml, iOSTypography.xaml, iOSStyles.xaml
  Resources/Android/              MaterialColors.xaml, MaterialTypography.xaml, MaterialStyles.xaml
samples/NativeStyles.Sample/      demo app: Buttons, Inputs, Selection, Feedback, Lists, Views, Typography
tests/Maui.NativeStyles.Tests/    xunit tests (net10.0)
```

## Getting started

1. Reference the library (project reference, or the `Maui.NativeStyles` NuGet package once published).
2. In the app `.csproj`: `<UseMaterial3>true</UseMaterial3>` and Microsoft.Maui.Controls >= 10.0.60 (this repo pins 10.0.101 in `Directory.Build.props`); remove the OpenSans `ConfigureFonts` registration so the system fonts are used.
3. Delete the template's `Resources/Styles/Styles.xaml` and `Colors.xaml`.
4. Merge the platform dictionary and register the handlers:

```xml
<!-- App.xaml -->
<Application xmlns:native="clr-namespace:NativeStyles;assembly=Maui.NativeStyles" ...>
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <native:NativeStyleDictionary />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

```csharp
// MauiProgram.cs
builder.UseMauiApp<App>()
       .UseNativeStyles();                                   // or .UseNativeStyles(o => o.AndroidDynamicColors = true)
```

5. Do not set `UIDesignRequiresCompatibility` in `Info.plist` (it disables Liquid Glass and is ignored from the iOS 27 SDK on).

## Shared StyleClass names

Same markup on both platforms; each class maps to the equivalent native role.
Every class is a XAML style that sets a typed attached property, so the two forms are equivalent and both
react to runtime changes:

```xml
<Button Text="Save" StyleClass="Filled, Large" />
<Button Text="Save" native:NativeButton.Kind="Filled" native:NativeButton.Size="Large" />
<Label Text="Title" native:NativeText.Weight="Semibold" />
<Entry Placeholder="Name" native:NativeEntry.IsPlain="True" />
```

| StyleClass | iOS 26 | Android (M3) |
|---|---|---|
| *(none)* | `UIButtonConfiguration.plain` (tinted text) | Filled (primary) |
| `Filled` | `filled` (tinted capsule, white label) | Filled |
| `Tonal` | `tinted` (15% tint) | Filled tonal (secondaryContainer) |
| `Outlined` | `gray` | Outlined: 1 dp `outlineVariant`, `onSurfaceVariant` label (M3 Expressive) |
| `Text` | `plain` | Text |
| `Glass` | `glass` (iOS 26) → `gray` on iOS 15–18 | Elevated |
| `GlassProminent` | `prominentGlass` → `filled` on iOS 15–18 | Filled |
| `Destructive` | `systemRed` tint | `error` / `onError` (error label with `Text`/`Outlined`) |
| (default) / `Small` / `Large` | 44 pt / `UIButtonConfigurationSize.Small` / `Large` | M3 Expressive S 40 dp (16 dp padding) / XS 32 dp (12 dp) / M 56 dp (24 dp, title medium) — all fully round |
| Entry / Editor (default) | borderless field on a filled shape: 52 pt row, 26 pt continuous corners (a capsule for one line), 20 pt text inset, clear button while editing — as in Settings › Name | Material 3 outlined text field (the `Editor`, a bare `EditText` in MAUI, gets the same outlined container: 4 dp corners, 1 dp `outline`, 2 dp `primary` when focused) |
| Entry / Editor `Expressive` | same as default (already a borderless field on a filled shape) | M3 Expressive containment: borderless field in a filled `surfaceContainerHighest` container with 28 dp corners |
| Entry / Editor `Plain` | no shape and no inset: the value part of a grouped row | no Material box |
| Picker / DatePicker / TimePicker | pull-down value with chevrons / compact `tertiarySystemFill` capsule (no text-field border) | secondary text with trailing menu arrow / calendar / clock icon, no underline; Material dialogs on tap |
| Label `Secondary` / `Tertiary` / `Accent` | `secondaryLabel` / `tertiaryLabel` / `systemBlue` | `onSurfaceVariant` / `outline` / `primary` |
| Label `Semibold` | SF Pro Semibold | Roboto Medium |
| Border `Card` / `Outlined` | inset grouped card, 26 pt radius (iOS 26), `secondarySystemGroupedBackground` | M3 card, 12 dp radius, `surfaceContainerLow` |
| Border `GroupedCell` | inset grouped section (26 pt radius, 20 pt margins) | M3 Expressive **segmented list**: transparent group clipped to 16 dp outer corners, 16 dp margins (baseline flat lists are no longer recommended by M3) |
| Grid `ListRow` | 52 pt row, `systemGray5` selection | expressive list item: `surfaceBright` container with 4 dp corners, 56 dp, 16×10 dp padding, 12 dp slot spacing; selected → `secondaryContainer` with 16 dp corners (`NativeList.ItemCornerRadius`) |
| BoxView `Separator` (alias `Gap`) | 0.5 pt `separator`, 16 pt inset both sides | 2 dp transparent gap between segmented items |
| BoxView `Divider` | full-width hairline | M3 divider: 1 dp `outlineVariant` |
| CheckBox `Leading` | — | leading control of a list row on the 16 dp keyline (native `RadioButton` rows are aligned automatically) |
| SearchBar / `Shell.SearchHandler` | 60 pt glass capsule / system navigation-bar search | M3 search bar: 56 dp pill, `surfaceContainerHigh` |
| `native:NativeSwipeItem` (in `SwipeItems`) | separated, continuously rounded action (26 pt, a capsule on a 52 pt row): `systemGray` / accent / `systemRed`, white content | M3 swipe-to-reveal button: fully round 56 dp, tonal (`secondaryContainer`) / `primary` / `error`, 4 dp apart |
| `native:SegmentedControl` | `UISegmentedControl` | M3 Expressive connected button group (tonal toggles, 8 dp inner corners, selected button fully round) |
| Shell top tabs | `UISegmentedControl` laid over MAUI's tab strip, inline title | M3 primary tabs: `primary` label and indicator, 1 dp `outlineVariant` divider, fixed tabs sharing the width (scrollable above four) |
| Shell flyout (`ItemTemplate` / `MenuItemTemplate`, Label `FlyoutHeader`) | sidebar: grouped background, 52 pt rows, continuous `systemFill` selection, accent icons, large-title header | M3 modal navigation drawer: `surfaceContainerLow` sheet at most 360 dp wide (56 dp of scrim left) with 16 dp trailing corners, 56 dp items inset 12 dp with a full-round `secondaryContainer` indicator, title-small headline |
| TabbedPage / NavigationPage | glass tab bar (supports `NativeShell.TabBarMinimizeBehavior`), transparent navigation bar in the page color with large titles | navigation bar at the bottom (flexible, 64 dp), flat app bar in the page color, trailing toolbar icons in `onSurfaceVariant` |
| RefreshView | system `UIRefreshControl` | `primary` arrow on a `surfaceContainerHigh` disc |
| Label `EmptyState` | title 3 semibold, `secondaryLabel`, centered | body large, `onSurfaceVariant`, centered |
| Button `Icon` (`NativeButton.TintsImage`) | the button image is a template in the label color (`UIButtonConfiguration`) | `MaterialButton` icon tinted with the label colors (enabled / disabled) |
| ImageButton `Icon` | template image in the tint color (visible in dark mode) | M3 standard icon button: icon in `onSurfaceVariant` |
| `native:NativeImage.TintColor` | template rendering with `tintColor` | `SrcIn` color filter |
| CarouselView + Border `CarouselItem` | leading-aligned paging: card on the 20 pt content margin, 12 pt gap, next card peeking from the trailing edge, no loop | M3 **uncontained carousel**: 16 dp leading padding, 8 dp gaps, 28 dp item corners, items bleed off the trailing edge, no loop |
| IndicatorView | native `UIPageControl` at its natural size (`label` / `tertiaryLabel`) | 8 dp dots, `primary` / `outlineVariant` (Material has no page-indicator component) |
| Label `Chevron` | `›` in `tertiaryLabel` | hidden (Material lists have no chevrons) |
| ContentPage `Grouped` | `systemGroupedBackground` | `surfaceContainer` page and app bar (tinted surface behind segmented lists, as in Android 16 Settings) |

Shared typography keys: `TitleXL`, `TitleL`, `TitleM`, `TitleS`, `Headline`, `BodyEmphasized`, `Body`, `BodySecondary`, `Caption`,
`SectionHeader`, `SectionFooter` (iOS 26: 17 pt semibold sentence-case header aligned with the row text; Android: `titleSmall` in `primary`).
Native keys: `LargeTitle … Caption2` (iOS), `DisplayLarge … LabelSmall` (Android).

Shared metric: `ContentMargin` (`Thickness`, 20 pt on iOS / 16 dp on Android) — the horizontal page margin for content placed outside a grouped section.

Shared color keys: `AccentColor`, `DestructiveColor`, `TextPrimary`, `TextSecondary`, `PageBackground`, `GroupedPageBackground`,
`CardBackground`, `DividerColor` (plus a `Dark` suffix). Native keys: `SystemBlue … SystemGray6`, `LabelColor`, `SeparatorColor`,
`SystemBackground`… (iOS 26 values, updated by Apple in June 2025: `systemBlue` is now `#0088FF`) and the M3 roles `Primary … OutlineVariant`.

## System colors

`{native:SystemColor Role}` produces a theme-aware binding whose light and dark values come from the platform:
`UIColor` system colors on iOS (they follow Increased Contrast) and Material theme attributes on Android.
With `AndroidDynamicColors = true` the Android values come from Material You (wallpaper-based) on Android 12+.
On the plain `net10.0` target, or when an attribute cannot be resolved, the static iOS 26 / Material 3 baseline palette is used.

```xml
<Label TextColor="{native:SystemColor TextSecondary}" />
<BoxView Color="{native:SystemColor Separator}" />
```

Roles: `Accent`, `OnAccent`, `Destructive`, `Success`, `Warning`, `TextPrimary`, `TextSecondary`, `TextTertiary`, `Placeholder`,
`Separator`, `PageBackground`, `GroupedBackground`, `CardBackground`, `GroupContainer`, `Fill`, `SecondaryFill`, `TonalContainer`, `OnTonalContainer`.
The same values are available in code through `SystemColors.Get(role)` and `SystemColors.Resolve(role)`.
The library's own styles use these roles, so an app that only uses the shared `StyleClass` names picks up dynamic colors automatically.

## Search on iOS 26

A standalone `SearchBar` is a `UISearchBar`, which iOS 26 draws as a 60 pt Liquid Glass capsule; UIKit lays it out
internally and the size cannot be reduced. The 44 pt search field seen in Settings or Mail is the navigation-bar
search, which in MAUI is `Shell.SearchHandler`:

```xml
<ContentPage ...>
    <Shell.SearchHandler>
        <SearchHandler Placeholder="Search" />
    </Shell.SearchHandler>
</ContentPage>
```

On iOS it renders the system search field under the large title; on Android it becomes the Material search view in the app bar.

## iOS 26 tab bar

```xml
<Shell native:NativeShell.TabBarMinimizeBehavior="OnScrollDown" ...>
```

The floating Liquid Glass tab bar collapses into a pill while the page scrolls down and expands again on scroll up.
The library registers the page's scroll view with UIKit on every navigation; no effect on iOS 15–18 or Android.

## GlassView

```xml
<native:GlassView GlassStyle="Regular" CornerRadius="-1" HorizontalOptions="Center" VerticalOptions="End">
    <HorizontalStackLayout Spacing="8">
        <Button Text="Share" StyleClass="Glass" />
        <Button Text="Continue" StyleClass="GlassProminent" />
    </HorizontalStackLayout>
</native:GlassView>
```

iOS 26: `UIVisualEffectView` + `UIGlassEffect` (`Regular`/`Clear`, `IsInteractive`, `TintColor`), shape via `UICornerConfiguration`
(capsule when `CornerRadius = -1`). iOS 15–18: `SystemMaterial` blur. Android: `surfaceContainerLow` surface with 3 dp elevation.
Per the HIG, use it only for floating controls above content, never in the content layer.

## Material 3 Expressive on Android

The Android styles follow the current recommendations on [m3.material.io](https://m3.material.io/components):
expressive segmented lists instead of baseline lists, the flexible navigation bar (64 dp, 56×32 dp active indicator,
`secondary` active label, `surfaceContainer` container) instead of the 80 dp baseline bar, the contained search bar,
16 dp button padding and the new outlined-button colors. Shell's bottom navigation, app bar and search view are native
views created by MAUI after navigation, so the library restyles them from an activity layout listener
(`ShellChromeStyler`). Text fields, cards (12 dp), switches, sliders and progress indicators already match through
`UseMaterial3` and are left untouched.

## Branding

```csharp
builder.UseNativeStyles(options =>
{
    options.Brand = new BrandPalette(Color.FromArgb("#0B7A75"), accentDark: Color.FromArgb("#3FC1B4"))
    {
        MaterialColorMatch = true,   // Android: keep the exact brand color as the light-theme primary
    };
});
```

**One palette, two resolutions.** A single brand definition is enough as input, but the two design languages use
color so differently that it resolves into two platform palettes:

| | iOS 26 | Android (Material 3) |
|---|---|---|
| What branding means | one tint color; backgrounds, labels and semantic colors stay Apple's | a whole tonal scheme derived from a seed: primary / secondary / tertiary families, tinted surfaces, outlines, light **and** dark |
| `Accent` | the window tint: buttons, links, selection, tab bar and toolbar items, alerts, and the `Accent` / `TonalContainer` roles | the seed. The library generates the scheme with Material's own algorithm (`SchemeContent`) |
| `AccentDark` | tint in dark mode (a brighter variant, as Apple does with its system colors); defaults to `Accent` | ignored: Material computes the dark tones from the seed |
| Native controls | follow the tint automatically (`UISwitch` stays green, as on iOS) | the generated scheme replaces Material's color resources for every activity (Android 11+), so Switch, CheckBox, RadioButton, Slider, progress indicators, text fields, the navigation bar, tabs and dialogs are branded too |

- `options.Brand.IOS.Accent` / `options.Brand.Android.Accent` give one platform a different brand color.
- `options.Brand.Set(SystemColorRole.Destructive, light, dark)` pins an individual role (it wins over everything, but only
  for colors resolved through `{native:SystemColor}`; native Android widgets keep the generated scheme).
- `MaterialColorMatch`: Material puts `primary` at a fixed tone of the seed's palette (usually darker than the brand color,
  which becomes `primaryContainer`). Set it to keep the exact color as the light-theme primary, like "color match" in
  Material Theme Builder.
- Every `{native:SystemColor}` role, the shared `AccentColor` / `DestructiveColor` resources, `NativeSwipeItem`,
  `SegmentedControl` and the Shell chrome follow the brand. The static per-platform keys (`Primary`, `SystemBlue`, …)
  stay the baseline palette.
- A brand wins over `AndroidDynamicColors` (a branded app keeps its colors instead of following the wallpaper).
- Below Android 11 only the colors resolved through `{native:SystemColor}` are branded. The Material color utilities
  used to generate the scheme are flagged by Google as internal API: the library guards their use and falls back to
  branding the accent only if they ever disappear.

## Swipe actions, segmented choice and navigation chrome

```xml
<SwipeView.RightItems>
    <!-- MAUI lays RightItems out from the trailing edge: declare the primary action first -->
    <SwipeItems Mode="Reveal">
        <native:NativeSwipeItem Text="Archive" Icon="archive.png" Role="Primary" Invoked="OnArchive" />
        <native:NativeSwipeItem Text="Delete" Role="Destructive" />
        <native:NativeSwipeItem Text="Flag" Icon="flag.png" />
    </SwipeItems>
</SwipeView.RightItems>

<native:SegmentedControl SelectedIndex="{Binding Range}">
    <x:String>Day</x:String>
    <x:String>Week</x:String>
    <x:String>Month</x:String>
</native:SegmentedControl>
```

`NativeSwipeItem` shows its icon when it has one (tinted with the label color, also for bitmaps through
`NativeImage.TintColor`) and its text otherwise. The Shell flyout, top tabs, `TabbedPage` and `NavigationPage` need no
markup: the implicit styles and the platform hooks restyle them. The legacy `ListView`, `TableView` and `Frame` are
intentionally not styled: use `CollectionView` / `GroupedCell` and `Border` (`Card`).

## Theme changes at runtime (Android)

MAUI handles the `uiMode` configuration change itself, so the activity survives a light/dark switch and every native
view keeps the colors it resolved when it was created: Switch, CheckBox, RadioButton, text fields, the navigation bar,
the status bar icons, dialogs. `{native:SystemColor}` values update, the rest does not.

With `AndroidRecreateOnThemeChange` (on by default) the library does what Android does by default: when the effective
app theme changes (system dark mode or `Application.UserAppTheme`) it recreates the activity, so everything is
re-themed. State is preserved:

- MAUI asks the app for a window again, and the standard template (`new Window(new AppShell())`) would answer with a new
  page tree. The library moves the page that was showing to the new window, so the selected tab, the navigation stack,
  control values and view models survive. Scroll positions and open native dialogs do not.
- Root pages that the app replaced earlier (login shell → main shell, Shell → TabbedPage) get their handlers
  disconnected before the recreate. MAUI 10 otherwise leaves a disposed Shell renderer registered on such a Shell,
  which throws on the following theme change; the library also clears that (private) observer list.
- Set `options.AndroidRecreateOnThemeChange = false` to keep MAUI's behavior.

## Known limitations

- **iOS `Switch`** may show glass artifacts on the active side ([dotnet/maui#34560](https://github.com/dotnet/maui/issues/34560)).
- **Do not set `Shell.TabBarBackgroundColor` on iOS**: it paints an opaque slab behind the glass tab bar ([#37423](https://github.com/dotnet/maui/issues/37423)).
- iOS `CheckBox` is drawn by MAUI (iOS has no checkbox); it is tinted with the accent color. iOS `RadioButton` uses a "row with checkmark" `ControlTemplate`.
- M3 Expressive widgets that need Material Components 1.13+ (loading indicator, wavy progress, button shape morph on press, button groups, split buttons) are not available: MAUI 10.0.101 ships Material Components 1.12 and the newer binding pulls conflicting AndroidX versions.
- Android `TabbedPage`: the library style sets `ToolbarPlacement=Bottom`, and MAUI's `SetToolbarPlacement()` / `On<Android>().SetToolbarPlacement()` throws when asked for a different value afterwards. To get Material top tabs use the XAML attribute or `page.SetValue(TabbedPage.ToolbarPlacementProperty, ToolbarPlacement.Top)`; they are styled as M3 primary tabs too.
- List rows have no pressed feedback (ripple / highlight): MAUI item containers own the touch handling, and making the row view clickable would break `CollectionView` selection.
- Android `Stepper` is a MAUI-drawn control (Android has no stepper); the library restyles its two buttons as Material 3 outlined icon buttons.
- The iOS 15–18 fallback paths are guarded by `OperatingSystem.IsIOSVersionAtLeast(26)` but were not exercised on an iOS 18 simulator during development.
- With `UseMaterial3`, Entry and the other input controls use internal `*Handler2` handlers; their mappers are reached through reflection (see `NativeStyles.Android.cs`), and a debug message is logged if the type is not found. These types become public in MAUI 11.
- All iOS 26 APIs are guarded by `OperatingSystem.IsIOSVersionAtLeast(26)`: on iOS 15–18 the same binary shows the classic appearance.

## Requirements

- .NET 10 SDK with the `maui` workload, Xcode 26 (iOS 26 SDK), Android SDK 36.
- Microsoft.Maui.Controls 10.0.60 or later (pinned to 10.0.101 via `MauiVersion` in `Directory.Build.props`).

## Building

```bash
dotnet build Maui.NativeStyles.slnx
```

```bash
dotnet test tests/Maui.NativeStyles.Tests/Maui.NativeStyles.Tests.csproj
```

```bash
dotnet pack src/Maui.NativeStyles/Maui.NativeStyles.csproj -c Release -o artifacts
```

Contributions follow gitflow; see [CONTRIBUTING.md](CONTRIBUTING.md). Changes are tracked in [CHANGELOG.md](CHANGELOG.md).

## Sources

Apple HIG ([Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons), [Color](https://developer.apple.com/design/human-interface-guidelines/color), [Typography](https://developer.apple.com/design/human-interface-guidelines/typography), [Materials](https://developer.apple.com/design/human-interface-guidelines/materials)),
Material 3 tokens ([material-web](https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192), [androidx](https://github.com/androidx/androidx/tree/androidx-main/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3/tokens)),
[Material 3 in .NET MAUI](https://learn.microsoft.com/dotnet/maui/user-interface/material-design), `UIGlassEffect` binding in Microsoft.iOS 26.5.

## License

MIT — see [LICENSE](LICENSE).
