namespace NativeStyles;

public class NativeStylesOptions
{
	/// <summary>
	/// Android 12+: apply Material You dynamic colors (derived from the wallpaper) to every MAUI activity and resolve
	/// <c>{native:SystemColor}</c> through them. Off by default so the Material 3 baseline palette is used.
	/// </summary>
	public bool AndroidDynamicColors { get; set; }

	/// <summary>
	/// The app's brand colors. On Android a brand seed takes precedence over <see cref="AndroidDynamicColors"/>
	/// (a branded app keeps its colors instead of following the wallpaper).
	/// </summary>
	public BrandPalette? Brand { get; set; }

	/// <summary>
	/// Android: recreate the activity when the app theme changes at runtime (system dark mode or
	/// <c>Application.UserAppTheme</c>). MAUI handles the uiMode configuration change itself, so the activity is kept and
	/// every native view (Switch, CheckBox, RadioButton, text fields, navigation bar, status bar icons, dialogs) keeps the
	/// colors it resolved when it was created. Recreating is what Android does by default and is the only way to re-theme
	/// them; the MAUI page tree, navigation state and view models are preserved. On by default.
	/// </summary>
	public bool AndroidRecreateOnThemeChange { get; set; } = true;

	/// <summary>
	/// iOS: register <c>NativeEntryHandler</c> for <see cref="Entry"/>: its <c>NativeTextField</c> insets the text and
	/// places the clear button like an iOS 26 Settings row. On by default.
	/// <para>
	/// The registration replaces the Entry handler registered before it, and a handler registered after it replaces
	/// <c>NativeEntryHandler</c>. Set this to false when the app or another library brings its own Entry handler: the
	/// Entry mapping then shapes the text field that handler creates without subclassing it (continuous corners through
	/// its layer, the 20 pt text margins as spacer <c>LeftView</c> / <c>RightView</c> views, added only while those are
	/// unused). UIKit hides the clear button while a right view shows, so the trailing spacer steps aside whenever the
	/// clear button can appear, and the button sits at UIKit's position near the edge instead of a Settings row's.
	/// </para>
	/// </summary>
	public bool ReplaceEntryHandler { get; set; } = true;

	/// <summary>True when both option sets configure the same styling (brands are compared by value).</summary>
	internal bool IsEquivalentTo(NativeStylesOptions other) =>
		AndroidDynamicColors == other.AndroidDynamicColors
		&& AndroidRecreateOnThemeChange == other.AndroidRecreateOnThemeChange
		&& ReplaceEntryHandler == other.ReplaceEntryHandler
		&& BrandsMatch(Brand, other.Brand);

	static bool BrandsMatch(BrandPalette? a, BrandPalette? b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a is null || b is null)
			return false;
		if (!Equals(a.Accent, b.Accent) || !Equals(a.AccentDark, b.AccentDark)
			|| a.MaterialColorMatch != b.MaterialColorMatch || a.TintsSwitches != b.TintsSwitches
			|| !Equals(a.IOS.Accent, b.IOS.Accent) || !Equals(a.IOS.AccentDark, b.IOS.AccentDark)
			|| !Equals(a.Android.Accent, b.Android.Accent) || !Equals(a.Android.AccentDark, b.Android.AccentDark))
			return false;
		foreach (var role in Enum.GetValues<SystemColorRole>())
		{
			var hasA = a.TryGetRole(role, out var colorsA);
			if (hasA != b.TryGetRole(role, out var colorsB) || (hasA && !colorsA.Equals(colorsB)))
				return false;
		}
		return true;
	}
}
