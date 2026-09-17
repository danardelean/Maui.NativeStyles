namespace NativeStyles;

/// <summary>
/// The app's brand colors. One definition serves both platforms, but it resolves differently on each because the design
/// languages use color differently:
/// <list type="bullet">
/// <item>iOS brands an app with a single tint: <see cref="Accent"/> (and <see cref="AccentDark"/>) become the window tint
/// and the <see cref="SystemColorRole.Accent"/> family. Backgrounds, labels and the semantic system colors stay Apple's.</item>
/// <item>Material 3 derives a whole tonal scheme from a seed (primary, secondary and tertiary families, tinted surfaces,
/// outlines; light and dark). <see cref="Accent"/> is that seed: the library generates the scheme, applies it to the
/// native widgets (Android 12+) and resolves every <see cref="SystemColorRole"/> from it.</item>
/// </list>
/// Use <see cref="IOS"/> / <see cref="Android"/> when a platform needs a different brand color, and
/// <see cref="Set"/> to pin individual roles.
/// </summary>
public sealed class BrandPalette
{
	readonly Dictionary<SystemColorRole, (Color Light, Color Dark)> _roles = [];

	public BrandPalette()
	{
	}

	public BrandPalette(Color accent, Color? accentDark = null)
	{
		Accent = accent;
		AccentDark = accentDark;
	}

	/// <summary>The brand color: iOS tint and Material seed.</summary>
	public Color? Accent { get; set; }

	/// <summary>
	/// The tint in dark mode on iOS (usually a brighter variant, as Apple does with its own system colors). Defaults to
	/// <see cref="Accent"/>. Material computes its own dark tones from the seed, so Android ignores it.
	/// </summary>
	public Color? AccentDark { get; set; }

	/// <summary>
	/// Android: keep the exact brand color as the light-theme <c>primary</c> (what Material Theme Builder calls
	/// "color match"). By default Material places primary at a fixed tone of the seed's palette, which is usually a
	/// little darker than the brand color, and uses the brand color itself for <c>primaryContainer</c>. The dark theme
	/// always uses the generated (lighter) tone, as a saturated brand color rarely has enough contrast on dark surfaces.
	/// </summary>
	public bool MaterialColorMatch { get; set; }

	/// <summary>
	/// iOS: draw switches in the brand color when they are on. Off by default: UISwitch does not follow the tint color
	/// and the Human Interface Guidelines say to "change the default color of a switch only if necessary. The default
	/// green color tends to work well in most cases, but you might want to use your app's accent color instead" (make
	/// sure it contrasts with the off state). A Switch that sets its own <c>OnColor</c> keeps it. Android switches always
	/// follow the generated Material scheme.
	/// </summary>
	public bool TintsSwitches { get; set; }

	/// <summary>iOS-only overrides of the brand color.</summary>
	public PlatformBrand IOS { get; } = new();

	/// <summary>Android-only overrides: <see cref="PlatformBrand.Accent"/> is the Material seed.</summary>
	public PlatformBrand Android { get; } = new();

	/// <summary>Pins one role to explicit colors on both platforms (wins over everything else).</summary>
	public BrandPalette Set(SystemColorRole role, Color light, Color? dark = null)
	{
		_roles[role] = (light, dark ?? light);
		return this;
	}

	internal bool TryGetRole(SystemColorRole role, out (Color Light, Color Dark) colors) => _roles.TryGetValue(role, out colors);

	/// <summary>The accent for the running platform (light, dark), or null when no brand color is set.</summary>
	internal (Color Light, Color Dark)? ResolveAccent()
	{
		var platform = DeviceInfo.Platform == DevicePlatform.iOS ? IOS : DeviceInfo.Platform == DevicePlatform.Android ? Android : null;
		var light = platform?.Accent ?? Accent;
		if (light is null)
			return null;
		return (light, platform?.AccentDark ?? (platform?.Accent is null ? AccentDark : null) ?? light);
	}
}

/// <summary>Per-platform brand color overrides.</summary>
public sealed class PlatformBrand
{
	public Color? Accent { get; set; }

	/// <summary>Dark-mode tint (iOS). Ignored on Android, where the dark scheme is generated from the seed.</summary>
	public Color? AccentDark { get; set; }
}
