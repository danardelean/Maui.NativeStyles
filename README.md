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
| `Outlined` | `gray` | Outlined (1 dp outline) |
| `Text` | `plain` | Text |
| `Glass` | `glass` (iOS 26) → `gray` on iOS 15–18 | Elevated |
| `GlassProminent` | `prominentGlass` → `filled` on iOS 15–18 | Filled |
| `Destructive` | `systemRed` tint | `error` / `onError` (error label with `Text`/`Outlined`) |
| `Small` / `Large` | `UIButtonConfigurationSize.Small` / `Large` | 32 dp / 56 dp (M3 Expressive sizes) |
| Entry `Plain` | `UITextBorderStyle.None` (grouped cells) | no Material box |
| Label `Secondary` / `Tertiary` / `Accent` | `secondaryLabel` / `tertiaryLabel` / `systemBlue` | `onSurfaceVariant` / `outline` / `primary` |
| Label `Semibold` | SF Pro Semibold | Roboto Medium |
| Border `Card` / `Outlined` | inset grouped card, 10 pt radius, `secondarySystemGroupedBackground` | M3 card, 12 dp radius, `surfaceContainerLow` |
| Border `GroupedCell` | inset grouped section (10 pt radius, 16 pt margins) | flat edge-to-edge list section (M3 lists are not cards) |
| Grid `ListRow` | 44 pt row, `systemGray5` selection | 56 dp row, 12% state layer |
| BoxView `Separator` | 0.5 pt `separator`, 16 pt inset | 1 dp `outlineVariant`, 16 dp inset |
| Label `Chevron` | `›` in `tertiaryLabel` | hidden (Material lists have no chevrons) |
| ContentPage `Grouped` | `systemGroupedBackground` | `surface` |

Shared typography keys: `TitleXL`, `TitleL`, `TitleM`, `TitleS`, `Headline`, `BodyEmphasized`, `Body`, `BodySecondary`, `Caption`,
`SectionHeader`, `SectionFooter`. Native keys: `LargeTitle … Caption2` (iOS), `DisplayLarge … LabelSmall` (Android).

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
`Separator`, `PageBackground`, `GroupedBackground`, `CardBackground`, `Fill`, `SecondaryFill`, `TonalContainer`, `OnTonalContainer`.
The same values are available in code through `SystemColors.Get(role)` and `SystemColors.Resolve(role)`.
The library's own styles use these roles, so an app that only uses the shared `StyleClass` names picks up dynamic colors automatically.

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

## Known limitations

- **Runtime theme switch on Android**: the status bar and the bottom navigation indicator do not update until the app restarts (MAUI Shell limitation).
- **iOS `Switch`** may show glass artifacts on the active side ([dotnet/maui#34560](https://github.com/dotnet/maui/issues/34560)).
- **Do not set `Shell.TabBarBackgroundColor` on iOS**: it paints an opaque slab behind the glass tab bar ([#37423](https://github.com/dotnet/maui/issues/37423)).
- iOS `CheckBox` is drawn by MAUI (iOS has no checkbox); it is tinted with the accent color. iOS `RadioButton` uses a "row with checkmark" `ControlTemplate`.
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
