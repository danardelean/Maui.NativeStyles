using Android.Content.Res;
using Android.Util;
using Android.Views;
using Google.Android.Material.Color;
using AApplication = Android.App.Application;
using AContext = Android.Content.Context;
using AColor = Android.Graphics.Color;

namespace NativeStyles;

public static partial class SystemColors
{
	/// <summary>
	/// When true (see <see cref="NativeStylesOptions.AndroidDynamicColors"/>), theme attributes are resolved through
	/// Material You (wallpaper-based) dynamic colors on Android 12+.
	/// </summary>
	internal static bool DynamicColorsEnabled { get; set; }

	static AContext? _lightContext, _darkContext;

	/// <summary>Material theme attributes resolved for both day and night configurations.</summary>
	static (Color Light, Color Dark)? ResolvePlatform(SystemColorRole role)
	{
		var attribute = role switch
		{
			SystemColorRole.Accent => Resource.Attribute.colorPrimary,
			SystemColorRole.OnAccent => Resource.Attribute.colorOnPrimary,
			SystemColorRole.Destructive => Resource.Attribute.colorError,
			SystemColorRole.TextPrimary => Resource.Attribute.colorOnSurface,
			SystemColorRole.TextSecondary => Resource.Attribute.colorOnSurfaceVariant,
			SystemColorRole.TextTertiary => Resource.Attribute.colorOutline,
			SystemColorRole.Placeholder => Resource.Attribute.colorOnSurfaceVariant,
			SystemColorRole.Separator => Resource.Attribute.colorOutlineVariant,
			SystemColorRole.PageBackground => Resource.Attribute.colorSurface,
			SystemColorRole.GroupedBackground => Resource.Attribute.colorSurface,
			SystemColorRole.CardBackground => Resource.Attribute.colorSurfaceContainerLow,
			SystemColorRole.Fill => Resource.Attribute.colorSurfaceContainerHighest,
			SystemColorRole.SecondaryFill => Resource.Attribute.colorSurfaceContainerHigh,
			SystemColorRole.TonalContainer => Resource.Attribute.colorSecondaryContainer,
			SystemColorRole.OnTonalContainer => Resource.Attribute.colorOnSecondaryContainer,
			_ => 0,
		};
		if (attribute == 0)
			return null;

		var light = ResolveAttribute(ThemedContext(dark: false), attribute);
		var dark = ResolveAttribute(ThemedContext(dark: true), attribute);
		return light is { } l && dark is { } d ? (l, d) : null;
	}

	/// <summary>A context carrying the MAUI Material 3 theme (plus the dynamic color overlay when enabled) for the requested night mode.</summary>
	internal static AContext ThemedContext(bool dark)
	{
		var cached = dark ? _darkContext : _lightContext;
		if (cached is not null)
			return cached;

		AContext context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? AApplication.Context;
		var configuration = new Configuration(context.Resources!.Configuration!);
		configuration.UiMode = (configuration.UiMode & ~UiMode.NightMask) | (dark ? UiMode.NightYes : UiMode.NightNo);
		context = context.CreateConfigurationContext(configuration)!;
		context = new ContextThemeWrapper(context, Resource.Style.Maui_Material3_Theme_NoActionBar);
		if (DynamicColorsEnabled)
			context = DynamicColors.WrapContextIfAvailable(context) ?? context;

		if (dark) _darkContext = context; else _lightContext = context;
		return context;
	}

	internal static Color? ResolveAttribute(AContext context, int attribute)
	{
		var value = new TypedValue();
		if (context.Theme?.ResolveAttribute(attribute, value, true) != true)
			return null;
		AColor color;
		if (value.Type >= DataType.FirstColorInt && value.Type <= DataType.LastColorInt)
			color = new AColor(value.Data);
		else if (value.ResourceId != 0)
			color = new AColor(AndroidX.Core.Content.ContextCompat.GetColor(context, value.ResourceId));
		else
			return null;
		return Color.FromRgba(color.R, color.G, color.B, color.A);
	}
}
