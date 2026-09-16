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
MauiNativeStyle.csproj                 net10.0-ios;net10.0-android, MauiVersion 10.0.101, UseMaterial3=true
App.xaml.cs                            merges the platform dictionaries with #if IOS / #if ANDROID
NativeStyles/NativeStylesExtensions.cs builder.UseNativeStyles() + StyleClass names
NativeStyles/GlassView.cs              Liquid Glass container (iOS 26) / elevated card (Android)
Platforms/iOS/NativeStyles.iOS.cs      Button mapper (UIButtonConfiguration/glass), Entry Plain, Label Semibold, GlassViewHandler
Platforms/Android/NativeStyles.Android.cs  Destructive+Text/Outlined mapper, Entry Plain, GlassViewHandler
Resources/Styles/iOS/                  iOSColors.xaml, iOSTypography.xaml, iOSStyles.xaml
Resources/Styles/Android/              MaterialColors.xaml, MaterialTypography.xaml, MaterialStyles.xaml
Pages/                                 demo app: Buttons, Inputs, Selection, Feedback, Lists, Typography
```

## Using the styles in an existing project

1. Copy `Resources/Styles/`, `NativeStyles/`, `Platforms/iOS/NativeStyles.iOS.cs` and `Platforms/Android/NativeStyles.Android.cs`.
2. In the `.csproj`: `<MauiVersion>10.0.101</MauiVersion>` (or >= 10.0.60) and `<UseMaterial3>true</UseMaterial3>`; remove the OpenSans `ConfigureFonts` registration.
3. Delete the template's `Resources/Styles/Styles.xaml` and `Colors.xaml` and the `MergedDictionaries` in `App.xaml`.
4. In `App.xaml.cs` merge the three platform dictionaries (see `App.xaml.cs`); in `MauiProgram.cs` call `.UseNativeStyles()`.
5. Do not set `UIDesignRequiresCompatibility` in `Info.plist` (it disables Liquid Glass and is ignored from the iOS 27 SDK on).

## Shared StyleClass names

Same markup on both platforms; each class maps to the equivalent native role.

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
| Border `Card` / `GroupedCell` / `Outlined` | inset grouped, 10 pt radius, `secondarySystemGroupedBackground` | M3 card, 12 dp radius, `surfaceContainerLow` |
| Grid `ListRow` | 44 pt row, `systemGray5` selection | 56 dp row, 12% state layer |
| BoxView `Separator` | 0.5 pt `separator`, 16 pt inset | 1 dp `outlineVariant` |
| Label `Chevron` | `›` in `tertiaryLabel` | hidden (Material lists have no chevrons) |
| ContentPage `Grouped` | `systemGroupedBackground` | `surface` |

Shared typography keys: `TitleXL`, `TitleL`, `TitleM`, `TitleS`, `Headline`, `BodyEmphasized`, `Body`, `BodySecondary`, `Caption`,
`SectionHeader`, `SectionFooter`. Native keys: `LargeTitle … Caption2` (iOS), `DisplayLarge … LabelSmall` (Android).

Shared color keys: `AccentColor`, `DestructiveColor`, `TextPrimary`, `TextSecondary`, `PageBackground`, `GroupedPageBackground`,
`CardBackground`, `DividerColor` (plus a `Dark` suffix). Native keys: `SystemBlue … SystemGray6`, `LabelColor`, `SeparatorColor`,
`SystemBackground`… (iOS 26 values, updated by Apple in June 2025: `systemBlue` is now `#0088FF`) and the M3 roles `Primary … OutlineVariant`.

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
- Android `Stepper` is a MAUI-drawn control (Android has no stepper).
- With `UseMaterial3`, Entry and the other input controls use internal `*Handler2` handlers; their mappers are reached through reflection (see `NativeStyles.Android.cs`). These types become public in MAUI 11.
- All iOS 26 APIs are guarded by `OperatingSystem.IsIOSVersionAtLeast(26)`: on iOS 15–18 the same binary shows the classic appearance.

## Requirements

- .NET 10 SDK with the `maui` workload, Xcode 26 (iOS 26 SDK), Android SDK 36.
- Microsoft.Maui.Controls 10.0.60 or later (pinned to 10.0.101 via `MauiVersion`).

## Sources

Apple HIG ([Buttons](https://developer.apple.com/design/human-interface-guidelines/buttons), [Color](https://developer.apple.com/design/human-interface-guidelines/color), [Typography](https://developer.apple.com/design/human-interface-guidelines/typography), [Materials](https://developer.apple.com/design/human-interface-guidelines/materials)),
Material 3 tokens ([material-web](https://github.com/material-components/material-web/tree/main/tokens/versions/v0_192), [androidx](https://github.com/androidx/androidx/tree/androidx-main/compose/material3/material3/src/commonMain/kotlin/androidx/compose/material3/tokens)),
[Material 3 in .NET MAUI](https://learn.microsoft.com/dotnet/maui/user-interface/material-design), `UIGlassEffect` binding in Microsoft.iOS 26.5.

## License

MIT — see [LICENSE](LICENSE).
