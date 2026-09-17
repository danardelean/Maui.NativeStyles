using Android.Content.Res;
using Android.Util;
using Android.Views;
using Google.Android.Material.Color;
using Microsoft.Maui.Platform;
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

	/// <summary>The cached themed contexts wrap an activity: drop them when it goes away.</summary>
	internal static void ResetThemedContexts() => _lightContext = _darkContext = null;

	/// <summary>The brand seed (ARGB) the Material scheme is generated from.</summary>
	internal static int? BrandSeed { get; set; }

	static bool _schemeUtilitiesUnavailable;

	/// <summary>
	/// Brand-derived roles on Android. The Material 3 scheme is generated from the seed with the algorithm Material
	/// Components uses for content-based dynamic colors (SchemeContent), so XAML-level colors and the themed native
	/// widgets agree on every Android version. Success and Warning have no Material role and keep their static values.
	/// </summary>
	static (Color Light, Color Dark)? ResolveBrandPlatform(SystemColorRole role, BrandPalette brand)
	{
		if (brand.ResolveAccent() is not { } accent)
			return null;
		if (!_schemeUtilitiesUnavailable)
		{
			try
			{
				if (BrandScheme.Resolve(role, accent.Light.ToPlatform().ToArgb(), brand.MaterialColorMatch) is { } generated)
					return generated;
			}
			catch (Exception e) when (e is Java.Lang.Throwable or MissingMethodException or TypeLoadException)
			{
				// com.google.android.material.color.utilities is restricted API: keep working if a release drops it
				_schemeUtilitiesUnavailable = true;
				System.Diagnostics.Debug.WriteLine($"[NativeStyles] Material color utilities unavailable: {e.Message}");
			}
		}
		// Fallback when the (restricted) Material color utilities are gone: only the accent itself can be branded
		return role switch
		{
			SystemColorRole.Accent => accent,
			SystemColorRole.OnAccent => accent.Light.GetLuminosity() > 0.6f ? (Colors.Black, Colors.Black) : (Colors.White, Colors.White),
			_ => null,
		};
	}

#pragma warning disable XAOBS001 // Restricted Material API, used deliberately (see ResolveBrandPlatform) and guarded at the call site
	static class BrandScheme
	{
		static Google.Android.Material.Color.Utilities.DynamicScheme? _light, _dark;
		static int _seed;

		static bool _colorMatch;

		/// <summary>Light-theme value of a generated color, honoring "color match" for primary / onPrimary.</summary>
		static int Light(string name, Google.Android.Material.Color.Utilities.DynamicColor color)
		{
			if (!_colorMatch || _light is null)
				return color.GetArgb(_light!);
			return name switch
			{
				"primary" => _seed,
				// Black or white, whichever contrasts more with the brand color
				"on_primary" => new AColor(_seed).GetBrightness() > 0.62f ? unchecked((int)0xFF000000) : unchecked((int)0xFFFFFFFF),
				_ => color.GetArgb(_light),
			};
		}

		public static (Color Light, Color Dark)? Resolve(SystemColorRole role, int seed, bool colorMatch)
		{
			_colorMatch = colorMatch;
			if (_light is null || _dark is null || _seed != seed)
			{
				var hct = Google.Android.Material.Color.Utilities.Hct.FromInt(seed);
				_light = new Google.Android.Material.Color.Utilities.SchemeContent(hct, false, 0);
				_dark = new Google.Android.Material.Color.Utilities.SchemeContent(hct, true, 0);
				_seed = seed;
			}

			var colors = new Google.Android.Material.Color.Utilities.MaterialDynamicColors();
			var dynamicColor = role switch
			{
				SystemColorRole.Accent => colors.Primary(),
				SystemColorRole.OnAccent => colors.OnPrimary(),
				SystemColorRole.Destructive => colors.Error(),
				SystemColorRole.TextPrimary => colors.OnSurface(),
				SystemColorRole.TextSecondary => colors.OnSurfaceVariant(),
				SystemColorRole.TextTertiary => colors.Outline(),
				SystemColorRole.Placeholder => colors.OnSurfaceVariant(),
				SystemColorRole.Separator => colors.OutlineVariant(),
				SystemColorRole.PageBackground => colors.Surface(),
				SystemColorRole.GroupedBackground => colors.SurfaceContainer(),
				SystemColorRole.CardBackground => colors.SurfaceContainerLow(),
				SystemColorRole.GroupContainer => colors.SurfaceBright(),
				SystemColorRole.Fill => colors.SurfaceContainerHighest(),
				SystemColorRole.SecondaryFill => colors.SurfaceContainerHigh(),
				SystemColorRole.TonalContainer => colors.SecondaryContainer(),
				SystemColorRole.OnTonalContainer => colors.OnSecondaryContainer(),
				SystemColorRole.Gray => colors.Outline(),
				_ => null,
			};
			if (dynamicColor is null)
				return null;
			var name = role switch { SystemColorRole.Accent => "primary", SystemColorRole.OnAccent => "on_primary", _ => string.Empty };
			return (ToColor(Light(name, dynamicColor)), ToColor(dynamicColor.GetArgb(_dark)));
		}

		static Color ToColor(int argb)
		{
			var color = new AColor(argb);
			return Color.FromRgba(color.R, color.G, color.B, color.A);
		}

		/// <summary>
		/// Replaces the values of Material's baseline system color resources (m3_sys_color_light_* / _dark_*), which the
		/// Material 3 theme attributes point to, with the generated scheme. Unlike a theme overlay this also reaches the
		/// widgets MAUI creates through its own ContextThemeWrapper (Switch, CheckBox, RadioButton, Slider, text fields).
		/// Android 11+ (ResourcesLoader).
		/// </summary>
		public static bool ApplyToResources(AContext context, int seed, bool colorMatch)
		{
			Resolve(SystemColorRole.Accent, seed, colorMatch); // builds the schemes
			if (_light is null || _dark is null || context.Resources is not { } resources || IColorResourcesOverride.Instance is not { } overrider)
				return false;

			var c = new Google.Android.Material.Color.Utilities.MaterialDynamicColors();
			var roles = new (string Name, Google.Android.Material.Color.Utilities.DynamicColor Color)[]
			{
				("primary", c.Primary()), ("on_primary", c.OnPrimary()), ("primary_container", c.PrimaryContainer()), ("on_primary_container", c.OnPrimaryContainer()),
				("inverse_primary", c.InversePrimary()),
				("secondary", c.Secondary()), ("on_secondary", c.OnSecondary()), ("secondary_container", c.SecondaryContainer()), ("on_secondary_container", c.OnSecondaryContainer()),
				("tertiary", c.Tertiary()), ("on_tertiary", c.OnTertiary()), ("tertiary_container", c.TertiaryContainer()), ("on_tertiary_container", c.OnTertiaryContainer()),
				("background", c.Background()), ("on_background", c.OnBackground()),
				("surface", c.Surface()), ("on_surface", c.OnSurface()), ("surface_variant", c.SurfaceVariant()), ("on_surface_variant", c.OnSurfaceVariant()),
				("inverse_surface", c.InverseSurface()), ("inverse_on_surface", c.InverseOnSurface()),
				("surface_bright", c.SurfaceBright()), ("surface_dim", c.SurfaceDim()),
				("surface_container", c.SurfaceContainer()), ("surface_container_low", c.SurfaceContainerLow()), ("surface_container_lowest", c.SurfaceContainerLowest()),
				("surface_container_high", c.SurfaceContainerHigh()), ("surface_container_highest", c.SurfaceContainerHighest()),
				("outline", c.Outline()), ("outline_variant", c.OutlineVariant()),
				("error", c.Error()), ("on_error", c.OnError()), ("error_container", c.ErrorContainer()), ("on_error_container", c.OnErrorContainer()),
			};

			// ApplyIfPossible also installs ThemeOverlay.Material3.PersonalizedColors on the context, which points the
			// theme attributes to the material_personalized_color_* resources. Those have no light/dark variants in an
			// override table, so they take the scheme of the current night mode (as Material Components itself does).
			var night = (resources.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
			var personalized = roles
				.Select(r => (Name: r.Name switch
				{
					"inverse_primary" => "primary_inverse",
					"inverse_surface" => "surface_inverse",
					"inverse_on_surface" => "on_surface_inverse",
					_ => r.Name,
				}, r.Color))
				.Concat(
				[
					("control_activated", c.ControlActivated()), ("control_highlight", c.ControlHighlight()), ("control_normal", c.ControlNormal()),
					("text_hint_foreground_inverse", c.TextHintInverse()), ("text_primary_inverse", c.TextPrimaryInverse()),
					("text_primary_inverse_disable_only", c.TextPrimaryInverseDisableOnly()),
					("text_secondary_and_tertiary_inverse", c.TextSecondaryAndTertiaryInverse()),
					("text_secondary_and_tertiary_inverse_disabled", c.TextSecondaryAndTertiaryInverseDisabled()),
				])
				.ToArray();

			// Material builds a resource table from the ids it is given and expects one complete, contiguous block per
			// call. The baseline m3_sys_color_* blocks are what MAUI's own ContextThemeWrapper (Switch, CheckBox,
			// RadioButton, Slider, text fields) resolves to; they do have light and dark resources.
			return Apply("material_personalized_color_", personalized, night ? _dark : _light)
				& Apply("m3_sys_color_light_", roles, _light)
				& Apply("m3_sys_color_dark_", roles, _dark);

			bool Apply(string prefix, (string Name, Google.Android.Material.Color.Utilities.DynamicColor Color)[] entries, Google.Android.Material.Color.Utilities.DynamicScheme scheme)
			{
				var values = new Dictionary<Java.Lang.Integer, Java.Lang.Integer>();
				foreach (var (name, color) in entries)
				{
					var id = resources.GetIdentifier(prefix + name, "color", context.PackageName);
					if (id != 0)
						values[Java.Lang.Integer.ValueOf(id)] = Java.Lang.Integer.ValueOf(ReferenceEquals(scheme, _light) ? Light(name, color) : color.GetArgb(scheme));
				}
				return values.Count > 0 && overrider.ApplyIfPossible(context, values);
			}
		}
	}
#pragma warning restore XAOBS001

	/// <summary>Brands the native Material widgets of an activity (Android 11+). Returns false when it is not possible.</summary>
	internal static bool ApplyBrandToActivity(Android.App.Activity activity)
	{
		if (BrandSeed is not { } seed || _schemeUtilitiesUnavailable || !OperatingSystem.IsAndroidVersionAtLeast(30))
			return false;
		try
		{
			return BrandScheme.ApplyToResources(activity, seed, Brand?.MaterialColorMatch == true);
		}
		catch (Exception e) when (e is Java.Lang.Throwable or MissingMethodException or TypeLoadException)
		{
			_schemeUtilitiesUnavailable = true;
			System.Diagnostics.Debug.WriteLine($"[NativeStyles] Material color resource override unavailable: {e.Message}");
			return false;
		}
	}

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
			SystemColorRole.GroupedBackground => Resource.Attribute.colorSurfaceContainer,
			SystemColorRole.CardBackground => Resource.Attribute.colorSurfaceContainerLow,
			SystemColorRole.GroupContainer => Resource.Attribute.colorSurfaceBright,
			SystemColorRole.Fill => Resource.Attribute.colorSurfaceContainerHighest,
			SystemColorRole.SecondaryFill => Resource.Attribute.colorSurfaceContainerHigh,
			SystemColorRole.TonalContainer => Resource.Attribute.colorSecondaryContainer,
			SystemColorRole.OnTonalContainer => Resource.Attribute.colorOnSecondaryContainer,
			SystemColorRole.Gray => Resource.Attribute.colorOutline,
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
