namespace NativeStyles;

public class NativeStylesOptions
{
	/// <summary>
	/// Android 12+: apply Material You dynamic colors (derived from the wallpaper) to every activity and resolve
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
}
