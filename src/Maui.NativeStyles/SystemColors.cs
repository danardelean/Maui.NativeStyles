namespace NativeStyles;

/// <summary>Semantic color roles resolved from the running platform theme.</summary>
public enum SystemColorRole
{
	/// <summary>iOS systemBlue / Android colorPrimary.</summary>
	Accent,
	/// <summary>Text on top of <see cref="Accent"/>.</summary>
	OnAccent,
	/// <summary>iOS systemRed / Android colorError.</summary>
	Destructive,
	/// <summary>iOS systemGreen / Android (no M3 role, static green).</summary>
	Success,
	/// <summary>iOS systemOrange / Android (no M3 role, static orange).</summary>
	Warning,
	/// <summary>iOS label / Android colorOnSurface.</summary>
	TextPrimary,
	/// <summary>iOS secondaryLabel / Android colorOnSurfaceVariant.</summary>
	TextSecondary,
	/// <summary>iOS tertiaryLabel / Android colorOutline.</summary>
	TextTertiary,
	/// <summary>iOS placeholderText / Android colorOnSurfaceVariant.</summary>
	Placeholder,
	/// <summary>iOS separator / Android colorOutlineVariant.</summary>
	Separator,
	/// <summary>iOS systemBackground / Android colorSurface.</summary>
	PageBackground,
	/// <summary>Page behind grouped lists: iOS systemGroupedBackground / Android colorSurfaceContainer (M3 Expressive lists sit on a tinted surface).</summary>
	GroupedBackground,
	/// <summary>iOS secondarySystemGroupedBackground / Android colorSurfaceContainerLow.</summary>
	CardBackground,
	/// <summary>List item container: iOS secondarySystemGroupedBackground / Android colorSurfaceBright (M3 Expressive segmented list items).</summary>
	GroupContainer,
	/// <summary>iOS systemFill / Android colorSurfaceContainerHighest.</summary>
	Fill,
	/// <summary>iOS tertiarySystemFill / Android colorSurfaceContainerHigh.</summary>
	SecondaryFill,
	/// <summary>iOS tinted button background (accent at 15%) / Android colorSecondaryContainer.</summary>
	TonalContainer,
	/// <summary>iOS accent / Android colorOnSecondaryContainer.</summary>
	OnTonalContainer,
	/// <summary>Opaque neutral for secondary actions: iOS systemGray / Android colorOutline.</summary>
	Gray,
}

/// <summary>
/// <c>{native:SystemColor Accent}</c>: an AppThemeBinding whose light and dark values come from the
/// platform (UIColor system colors on iOS, Material theme attributes on Android, including Material You dynamic
/// colors when enabled). Falls back to the static iOS 26 / Material 3 baseline palette when the platform cannot
/// answer (for example on the plain net10.0 target).
/// </summary>
[ContentProperty(nameof(Role))]
[AcceptEmptyServiceProvider]
public class SystemColorExtension : IMarkupExtension<BindingBase>
{
	public SystemColorRole Role { get; set; }

	public BindingBase ProvideValue(IServiceProvider serviceProvider) => SystemColors.GetBinding(Role);

	object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}

/// <summary>Resolves <see cref="SystemColorRole"/> values from the running platform theme.</summary>
public static partial class SystemColors
{
	/// <summary>Theme-aware binding (light + dark) for a role, the same object an AppThemeBinding markup extension produces.</summary>
	public static BindingBase GetBinding(SystemColorRole role)
	{
		var (light, dark) = Resolve(role);
		return AppThemeBindingFactory.Create(light, dark);
	}

	/// <summary>
	/// Microsoft.Maui.Controls.AppThemeBinding is internal and AppThemeBindingExtension needs a XAML service
	/// provider, so the binding is instantiated through its parameterless constructor and Light/Dark properties.
	/// </summary>
	static class AppThemeBindingFactory
	{
		static readonly Type BindingType = typeof(BindingBase).Assembly.GetType("Microsoft.Maui.Controls.AppThemeBinding")
			?? throw new InvalidOperationException("Microsoft.Maui.Controls.AppThemeBinding not found.");
		static readonly System.Reflection.PropertyInfo LightProperty = BindingType.GetProperty("Light")!;
		static readonly System.Reflection.PropertyInfo DarkProperty = BindingType.GetProperty("Dark")!;

		public static BindingBase Create(Color light, Color dark)
		{
			var binding = (BindingBase)Activator.CreateInstance(BindingType)!;
			LightProperty.SetValue(binding, light);
			DarkProperty.SetValue(binding, dark);
			return binding;
		}
	}

	/// <summary>The color for the current app theme.</summary>
	public static Color Get(SystemColorRole role)
	{
		var (light, dark) = Resolve(role);
		return Application.Current?.RequestedTheme == AppTheme.Dark ? dark : light;
	}

	/// <summary>Light and dark values: platform first, static baseline palette as fallback.</summary>
	public static (Color Light, Color Dark) Resolve(SystemColorRole role)
	{
		var platform = ResolvePlatform(role);
		var fallback = Fallback(role);
		return (platform?.Light ?? fallback.Light, platform?.Dark ?? fallback.Dark);
	}

	/// <summary>Static baseline palette: iOS 26 system colors on iOS, Material 3 baseline on Android and elsewhere.</summary>
	public static (Color Light, Color Dark) Fallback(SystemColorRole role) =>
		DeviceInfo.Platform == DevicePlatform.iOS ? IOSFallback(role) : MaterialFallback(role);

	static (Color, Color) IOSFallback(SystemColorRole role) => role switch
	{
		SystemColorRole.Accent => Pair("#0088FF", "#0091FF"),
		SystemColorRole.OnAccent => Pair("#FFFFFF", "#FFFFFF"),
		SystemColorRole.Destructive => Pair("#FF383C", "#FF4245"),
		SystemColorRole.Success => Pair("#34C759", "#30D158"),
		SystemColorRole.Warning => Pair("#FF8D28", "#FF9230"),
		SystemColorRole.TextPrimary => Pair("#000000", "#FFFFFF"),
		SystemColorRole.TextSecondary => Pair("#993C3C43", "#99EBEBF5"),
		SystemColorRole.TextTertiary => Pair("#4D3C3C43", "#4DEBEBF5"),
		SystemColorRole.Placeholder => Pair("#4D3C3C43", "#4DEBEBF5"),
		SystemColorRole.Separator => Pair("#4A3C3C43", "#99545458"),
		SystemColorRole.PageBackground => Pair("#FFFFFF", "#000000"),
		SystemColorRole.GroupedBackground => Pair("#F2F2F7", "#000000"),
		SystemColorRole.CardBackground => Pair("#FFFFFF", "#1C1C1E"),
		SystemColorRole.GroupContainer => Pair("#FFFFFF", "#1C1C1E"),
		SystemColorRole.Fill => Pair("#33787880", "#5C787880"),
		SystemColorRole.SecondaryFill => Pair("#1F767680", "#3D767680"),
		SystemColorRole.TonalContainer => Pair("#260088FF", "#260091FF"),
		SystemColorRole.OnTonalContainer => Pair("#0088FF", "#0091FF"),
		SystemColorRole.Gray => Pair("#8E8E93", "#8E8E93"),
		_ => Pair("#000000", "#FFFFFF"),
	};

	static (Color, Color) MaterialFallback(SystemColorRole role) => role switch
	{
		SystemColorRole.Accent => Pair("#6750A4", "#D0BCFF"),
		SystemColorRole.OnAccent => Pair("#FFFFFF", "#381E72"),
		SystemColorRole.Destructive => Pair("#B3261E", "#F2B8B5"),
		SystemColorRole.Success => Pair("#2E7D32", "#81C784"),
		SystemColorRole.Warning => Pair("#EF6C00", "#FFB74D"),
		SystemColorRole.TextPrimary => Pair("#1D1B20", "#E6E0E9"),
		SystemColorRole.TextSecondary => Pair("#49454F", "#CAC4D0"),
		SystemColorRole.TextTertiary => Pair("#79747E", "#938F99"),
		SystemColorRole.Placeholder => Pair("#49454F", "#CAC4D0"),
		SystemColorRole.Separator => Pair("#CAC4D0", "#49454F"),
		SystemColorRole.PageBackground => Pair("#FEF7FF", "#141218"),
		SystemColorRole.GroupedBackground => Pair("#F3EDF7", "#211F26"),
		SystemColorRole.CardBackground => Pair("#F7F2FA", "#1D1B20"),
		SystemColorRole.GroupContainer => Pair("#FEF7FF", "#3B383E"),
		SystemColorRole.Fill => Pair("#E6E0E9", "#36343B"),
		SystemColorRole.SecondaryFill => Pair("#ECE6F0", "#2B2930"),
		SystemColorRole.TonalContainer => Pair("#E8DEF8", "#4A4458"),
		SystemColorRole.OnTonalContainer => Pair("#1D192B", "#E8DEF8"),
		SystemColorRole.Gray => Pair("#79747E", "#938F99"),
		_ => Pair("#1D1B20", "#E6E0E9"),
	};

	static (Color, Color) Pair(string light, string dark) => (Color.FromArgb(light), Color.FromArgb(dark));

#if !IOS && !ANDROID
	static (Color Light, Color Dark)? ResolvePlatform(SystemColorRole role) => null;
#endif
}
